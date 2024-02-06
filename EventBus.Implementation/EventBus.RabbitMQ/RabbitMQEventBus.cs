//*********************************************************************************************
//* File             :   RabbitMQEventBus.cs
//* Author           :   Rout, Sukanta  
//* Date             :   18/8/2023
//* Description      :   Initial version
//* Version          :   1.0
//*-------------------------------------------------------------------------------------------
//* dd-MMM-yyyy	: Version 1.x, Changed By : xxx
//*
//*                 - 1)
//*                 - 2)
//*                 - 3)
//*                 - 4)
//*
//*********************************************************************************************

using Sukanta.EventBus.Abstraction.Bus;
using Sukanta.EventBus.Abstraction.Common;
using Sukanta.EventBus.Abstraction.Events;
using Sukanta.EventBus.Abstraction.SubscriptionManager;
using Sukanta.Resiliency;
using Sukanta.Resiliency.Abstraction;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using static Sukanta.EventBus.Abstraction.SubscriptionManager.EventBusSubscriptionManager;

namespace Sukanta.EventBus.RabbitMQ
{
    /// <summary>
    /// RabbitMQ EventBus Implementation
    /// </summary>
    public class RabbitMQEventBus : IEventBus, IDisposable
    {
        const string EXCHANGE_NAME = "EventBus.direct";
        const string EXCHANGE_TYPE = "direct";
        private readonly IRabbitMQConnection _rabbitMQConnection;
        private readonly IEventBusSubscriptionManager _subsManager;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IResilientPolicy _resilientPolicy;
        private readonly int _retryCount;
        private IModel _consumerChannel;
        private string _queueName;
        private readonly ILogger _logger;

        private static ConcurrentDictionary<string, IModel> ChannelAndQueue = new ConcurrentDictionary<string, IModel>();

        /// <summary>
        /// Unique ConnectionId
        /// </summary>
        public string ConnectionId { get; }

        /// <summary>
        /// Is connected to messaging bus
        /// </summary>
        public bool IsConnected => _rabbitMQConnection != null && _rabbitMQConnection.IsConnected;

        /// <summary>
        /// CommunicationBus is RabbitMQ
        /// </summary>
        public CommunicationBus CommunicationMode => CommunicationBus.RabbitMQ;

        /// <summary>
        /// RabbitMQ EventBus
        /// </summary>
        /// <param name="rabbitMQConnection"></param>
        /// <param name="subsManager"></param>
        /// <param name="serviceScopeFactory"></param>
        /// <param name="logger"></param>
        /// <param name="queueName"></param>
        /// <param name="resilientPolicy"></param>
        /// <param name="retryCount"></param>
        public RabbitMQEventBus(IRabbitMQConnection rabbitMQConnection, IEventBusSubscriptionManager subsManager, IServiceScopeFactory serviceScopeFactory,
            ILogger logger, string queueName = null, IResilientPolicy resilientPolicy = null, int retryCount = 3)
        {
            _rabbitMQConnection = rabbitMQConnection ?? throw new ArgumentNullException(nameof(rabbitMQConnection));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _subsManager = subsManager ?? new EventBusSubscriptionManager();

            //If a queue name provided in the constructor
            if (!string.IsNullOrEmpty(queueName))
            {
                _queueName = queueName;
                _consumerChannel = CreateConsumerChannel();
            }

            _retryCount = retryCount;
            _subsManager.OnEventRemoved += SubsManager_OnEventRemoved;
            _serviceScopeFactory = serviceScopeFactory;
            ConnectionId = Guid.NewGuid().ToString();
            _resilientPolicy = resilientPolicy ?? new RabbitMQResilientPolicy(_logger, _retryCount);
        }

        /// <summary>
        /// Event removed handler
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SubsManager_OnEventRemoved(object sender, EventMetadataEventArgs e)
        {
            if (!_rabbitMQConnection.IsConnected)
            {
                _rabbitMQConnection.TryConnect();
            }

            using (var channel = _rabbitMQConnection.CreateModel())
            {
                if (!string.IsNullOrEmpty(_queueName))
                {
                    channel.QueueUnbind(queue: _queueName,
                        exchange: EXCHANGE_NAME,
                        routingKey: e.EventName);
                }
                else
                {
                    channel.QueueUnbind(queue: e.QueueName,
                        exchange: EXCHANGE_NAME,
                        routingKey: e.EventName);

                    try
                    {
                        _logger.Information("Closing channel for queue {queueName}", e.QueueName);
                        ChannelAndQueue.TryGetValue(e.QueueName, out IModel channelforQueue);
                        channelforQueue?.Close();

                    }
                    catch
                    {
                        _logger.Warning("Error closing channel for queue {queueName}", e.QueueName);
                    }
                }

                if (_subsManager.IsSubscriptionsEmpty)
                {
                    //If subscription is empty delete all queuess
                    try
                    {
                        if (!string.IsNullOrEmpty(_queueName))
                        {
                            channel.QueueDelete(_queueName, true, true);
                            _logger.Information("Deleted rabbitmq queue {queueName}", _queueName);
                        }
                        else
                        {
                            channel.QueueDelete(e.QueueName, true, true);
                            _logger.Information("Deleted rabbitmq queue {queueName}", e.QueueName);

                        }
                    }
                    catch (Exception exp)
                    {
                        _logger.Warning("Error deleting rabbitmq queue ", exp.Message);
                    }

                    _queueName = string.Empty;
                    _consumerChannel?.Close();
                }
            }
        }


        /// <summary>
        /// Publish an event to the RabbitMQ queue
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="event"></param>
        public void Publish<T>(T @event) where T : Event
        {
            try
            {
                if (!_rabbitMQConnection.IsConnected)
                {
                    _rabbitMQConnection.TryConnect();
                }

                if (_rabbitMQConnection.IsConnected)
                {
                    //Using retry policy
                    var policy = Policy.Handle<BrokerUnreachableException>()
                     .Or<SocketException>()
                     .WaitAndRetry(_retryCount, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), (ex, time) =>
                     {
                         _logger.Warning(ex, "Could not publish event: {EventId} after {Timeout}s ({ExceptionMessage})", @event.Id, $"{time.TotalSeconds:n1}", ex.Message);
                     });

                    //Event name
                    var eventName = @event.GetType().Name;

                    _logger.Debug("Creating RabbitMQ channel to publish event: {EventId} ({EventName})", @event.Id, eventName);

                    using (var channel = _rabbitMQConnection.CreateModel())
                    {
                        _logger.Debug("Declaring RabbitMQ exchange to publish event: {EventId}", @event.Id);

                        channel.ExchangeDeclare(exchange: EXCHANGE_NAME, type: EXCHANGE_TYPE);

                        //if no queue is defined events are published to Queue based on EventName
                        if (string.IsNullOrEmpty(_queueName))
                        {
                            channel.QueueDeclare(eventName, true, false, false, null);
                        }

                        string eventData = JsonConvert.SerializeObject(@event);
                        byte[] body = Encoding.UTF8.GetBytes(eventData);

                        policy.Execute(() =>
                        {
                            var properties = channel.CreateBasicProperties();
                            properties.DeliveryMode = 2; // persistent

                            channel.BasicPublish(
                                exchange: EXCHANGE_NAME,
                                routingKey: eventName,
                                mandatory: true,
                                basicProperties: properties,
                                body: body);
                        });
                    }

                    _logger.Information("Published event with id : {EventId}", @event.Id);
                }
                else
                {
                    _logger.Error("Connection with server broken, couldn't published event with id : {EventId}", @event.Id);
                }
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        /// Publish to an specific queue
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="event"></param>
        /// <param name="queueName"></param>
        public void Publish<T>(T @event, string queueName) where T : Event
        {
            try
            {
                if (!_rabbitMQConnection.IsConnected)
                {
                    _rabbitMQConnection.TryConnect();
                }

                if (_rabbitMQConnection.IsConnected)
                {
                    //Using retry policy
                    var policy = Policy.Handle<BrokerUnreachableException>()
                     .Or<SocketException>()
                     .WaitAndRetry(_retryCount, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), (ex, time) =>
                     {
                         _logger.Warning(ex, "Could not publish event: {EventId} to queue {queueName} after {Timeout}s ({ExceptionMessage})", @event.Id, queueName, $"{time.TotalSeconds:n1}", ex.Message);
                     });

                    //Event name
                    var eventName = @event.GetType().Name;

                    _logger.Debug("Creating RabbitMQ channel to publish event: {EventId} ({EventName})", @event.Id, eventName);

                    using (var channel = _rabbitMQConnection.CreateModel())
                    {
                        _logger.Debug("Declaring RabbitMQ exchange to publish event: {EventId}", @event.Id);

                        channel.ExchangeDeclare(exchange: EXCHANGE_NAME, type: EXCHANGE_TYPE);

                        string eventData = JsonConvert.SerializeObject(@event);
                        byte[] body = Encoding.UTF8.GetBytes(eventData);

                        policy.Execute(() =>
                        {
                            var properties = channel.CreateBasicProperties();
                            properties.DeliveryMode = 2; // persistent
                            properties.ReplyTo = queueName;// used to pass queueName info

                            channel.QueueDeclare(queue: queueName,
                                     durable: true,
                                     exclusive: false,
                                     autoDelete: false,
                                     arguments: null);

                            channel.BasicPublish(
                                exchange: EXCHANGE_NAME,
                                routingKey: eventName,
                                mandatory: true,
                                basicProperties: properties,
                                body: body);

                        });
                    }

                    _logger.Information("Published event with id {EventId} to queue {queueName}", @event.Id, queueName);
                }
                else
                {
                    _logger.Error("Connection with server broken, couldn't published event with id : {EventId}", @event.Id);
                }
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        /// Subscribe from a specific queue
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TH"></typeparam>
        /// <param name="queueName"></param>
        public void Subscribe<T, TH>(string queueName)
            where T : Event
            where TH : IEventHandler<T>
        {
            IModel channel = null;

            try
            {
                var eventName = _subsManager.GetEventKey<T>();

                //Subscribe and consume message from the spcified rabbitmq queue
                channel = DoSubscriptionWithRabbitMQ(eventName, queueName);

                if (_subsManager.GetEventByName(eventName) == null && channel != null)
                {
                    //Start RabbitMQ queue consumption
                    StartBasicConsume(queueName, channel);
                }

                try
                {
                    _logger.Information("Subscribing to event {EventName} with {EventHandler} from queue {queueName}", eventName, typeof(TH).Name, queueName);

                    //Add to subscription
                    _subsManager.AddSubscription<T, TH>(queueName);
                }
                catch (Exception)
                {
                    _logger.Warning("{eventName} already subscribed.", eventName);

                    //Close the additional channel created
                    channel?.Close();
                    _logger.Information("Closed additional channel created due to duplicate subscriptions");
                }
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        /// Subscribe events
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TH"></typeparam>
        public void Subscribe<T, TH>()
            where T : Event
            where TH : IEventHandler<T>
        {
            try
            {
                var eventName = _subsManager.GetEventKey<T>();

                //RabbitMQ Subscription
                DoSubscriptionWithRabbitMQ(eventName);

                try
                {
                    _logger.Information("Subscribing to event {EventName} with {EventHandler}", eventName, typeof(TH).Name);

                    //Add to subscription
                    _subsManager.AddSubscription<T, TH>();
                }
                catch (Exception)
                {
                    _logger.Warning("{eventName} already subscribed.", eventName);
                }

                //Start RabbitMQ queue consumption
                StartBasicConsume<T>();
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        ///  Internal Subscription with RabbitMQ from a specific queue
        /// </summary>
        /// <param name="eventName"></param>
        /// <param name="queueName"></param>
        private IModel DoSubscriptionWithRabbitMQ(string eventName, string queueName)
        {
            var containsKey = _subsManager.HasSubscriptionsForEvent(eventName);

            IModel channel = null;

            if (!containsKey)
            {
                if (!_rabbitMQConnection.IsConnected)
                {
                    _rabbitMQConnection.TryConnect();
                }

                channel = _rabbitMQConnection.CreateModel();

                //store the channel and queue in a dictionary
                ChannelAndQueue.TryAdd(queueName, channel);

                channel.ExchangeDeclare(exchange: EXCHANGE_NAME, type: EXCHANGE_TYPE);

                channel.QueueDeclare(queue: queueName,
                         durable: true,
                         exclusive: false,
                         autoDelete: false,
                         arguments: null);

                channel.QueueBind(queue: queueName, exchange: EXCHANGE_NAME, routingKey: eventName);

                channel.CallbackException += (sender, eventArg) =>
                {
                    _logger.Warning(eventArg.Exception, "Recreating RabbitMQ channel for queue {queueName}", queueName);

                    ReInitializeConsumerChannelForQueue(queueName, channel, eventName);
                };
            }

            return channel;
        }

        /// <summary>
        /// Internal Subscription with RabbitMQ
        /// </summary>
        /// <param name="eventName"></param>
        private void DoSubscriptionWithRabbitMQ(string eventName)
        {
            var containsKey = _subsManager.HasSubscriptionsForEvent(eventName);

            if (!containsKey)
            {
                if (!_rabbitMQConnection.IsConnected)
                {
                    _rabbitMQConnection.TryConnect();
                }

                using (var channel = _rabbitMQConnection.CreateModel())
                {
                    if (string.IsNullOrEmpty(_queueName))
                    {
                        channel.ExchangeDeclare(exchange: EXCHANGE_NAME, type: EXCHANGE_TYPE);

                        channel.QueueDeclare(queue: eventName,
                                 durable: true,
                                 exclusive: false,
                                 autoDelete: false,
                                 arguments: null);

                        channel.QueueBind(queue: eventName, exchange: EXCHANGE_NAME, routingKey: eventName);
                    }
                    else
                    {
                        channel.QueueBind(queue: _queueName, exchange: EXCHANGE_NAME, routingKey: eventName);
                    }
                }
            }
        }

        /// <summary>
        /// ReInitializeConsumerChannel
        /// </summary>
        /// <param name="queueName"></param>
        /// <param name="channel"></param>
        /// <param name="eventName"></param>
        private void ReInitializeConsumerChannelForQueue(string queueName, IModel channel, string eventName)
        {
            channel.Dispose();

            var channelNew = _rabbitMQConnection.CreateModel();
            channel.QueueBind(queue: queueName, exchange: EXCHANGE_NAME, routingKey: eventName);

            StartBasicConsume(queueName, channelNew);
        }

        /// <summary>
        /// Unsubscribe events from subscription handler
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TH"></typeparam>
        /// <param name="queueName"></param>
        public void Unsubscribe<T, TH>(string queueName = null)
            where T : Event
            where TH : IEventHandler<T>
        {
            var eventName = _subsManager.GetEventKey<T>();
            _subsManager.RemoveSubscription<T, TH>(queueName);

            if (string.IsNullOrEmpty(queueName))
            {
                _logger.Information("Unsubscribed from event {EventName}", eventName);
            }
            else
            {
                _logger.Information("Unsubscribed from event {EventName} from queue {queueName}", eventName, queueName);
            }
        }

        /// <summary>
        /// Start BasicConsume from RabbitMQ queue
        /// </summary>
        private void StartBasicConsume<T>() where T : Event
        {
            _logger.Information("Starting basic consume");

            if (!_rabbitMQConnection.IsConnected)
            {
                _rabbitMQConnection.TryConnect();
            }

            if (_rabbitMQConnection.IsConnected)
            {
                if (string.IsNullOrEmpty(_queueName))
                {
                    var channel = _rabbitMQConnection.CreateModel();

                    var consumerEvent = new AsyncEventingBasicConsumer(channel);

                    consumerEvent.Received += ConsumerEvent_Received;

                    var eventName = typeof(T).Name;

                    channel.BasicConsume(
                   queue: eventName,
                   autoAck: false,
                   consumer: consumerEvent);
                }
                else
                {
                    if (_consumerChannel != null)
                    {
                        var consumerEvent = new AsyncEventingBasicConsumer(_consumerChannel);

                        consumerEvent.Received += ConsumerEvent_Received;

                        _consumerChannel.BasicConsume(
                            queue: _queueName,
                            autoAck: false,
                            consumer: consumerEvent);
                    }
                    else
                    {
                        _logger.Error("StartBasicConsume can't trigger as there is no consumerChannel");
                    }
                }
            }
            else
            {
                _logger.Error("Connection with server broken, couldn't subscribe to event");
            }
        }

        /// <summary>
        /// StartBasicConsume from sigle queue
        /// </summary>
        private void StartBasicConsume()
        {
            _logger.Information("Starting basic consume");

            if (_consumerChannel != null)
            {
                var consumerEvent = new AsyncEventingBasicConsumer(_consumerChannel);

                consumerEvent.Received += ConsumerEvent_Received;

                _consumerChannel.BasicConsume(
                    queue: _queueName,
                    autoAck: false,
                    consumer: consumerEvent);
            }
            else
            {
                _logger.Error("StartBasicConsume can't trigger as there is no consumerChannel");
            }
        }

        /// <summary>
        ///  Start BasicConsume from RabbitMQ queue
        /// </summary>
        /// <param name="queueName"></param>
        /// <param name="channel"></param>
        private void StartBasicConsume(string queueName, IModel channel)
        {
            _logger.Information("Starting basic consume from queue {queueName}", queueName);

            if (channel != null)
            {
                var consumerEvent = new AsyncEventingBasicConsumer(channel);

                consumerEvent.Received += ConsumerEvent_Received;

                channel.BasicConsume(
                    queue: queueName,
                    autoAck: true,
                    consumer: consumerEvent);
            }
            else
            {
                _logger.Error("StartBasicConsume can't trigger as channel is null for queue {queueName}.", queueName);
            }
        }

        /// <summary>
        /// Event received for processing/consumption by the consumer application
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="eventArgs"></param>
        /// <returns></returns>
        private async Task ConsumerEvent_Received(object sender, BasicDeliverEventArgs eventArgs)
        {
            string eventName = eventArgs.RoutingKey;
            string queueName = eventArgs.BasicProperties.ReplyTo;
            string eventData = Encoding.UTF8.GetString(eventArgs.Body.ToArray());//change for update made

            try
            {
                await ProcessEvent(eventName, eventData, queueName).ConfigureAwait(false);
            }
            catch (Exception exp)
            {
                _logger.Warning(exp, "Error processing event {eventName}", eventData);
            }

            //Ack to the broker
            _consumerChannel?.BasicAck(eventArgs.DeliveryTag, multiple: false);
        }


        /// <summary>
        ///Create ConsumerChannel
        /// </summary>
        /// <returns></returns>
        private IModel CreateConsumerChannel()
        {
            if (!_rabbitMQConnection.IsConnected)
            {
                _rabbitMQConnection.TryConnect();
            }

            _logger.Information("Creating consumer channel");

            var channel = _rabbitMQConnection.CreateModel();

            channel.ExchangeDeclare(exchange: EXCHANGE_NAME, type: EXCHANGE_TYPE);

            channel.QueueDeclare(queue: _queueName,
                                 durable: true,
                                 exclusive: false,
                                 autoDelete: false,
                                 arguments: null);

            channel.CallbackException += (sender, eventArg) =>
            {
                _logger.Warning(eventArg.Exception, "Recreating consumer channel");
                ReInitializeConsumerChannel();
            };

            return channel;
        }

        /// <summary>
        /// ReInitialize ConsumerChannel (Dispose and create new)
        /// Start BasicConsumption again
        /// </summary>
        private void ReInitializeConsumerChannel()
        {
            _consumerChannel.Dispose();
            _consumerChannel = CreateConsumerChannel();
            StartBasicConsume();
        }

        /// <summary>
        /// Process the Event by the respective handler
        /// </summary>
        /// <param name="eventName"></param>
        /// <param name="message"></param>
        /// <param name="queueName"></param>
        /// <returns></returns>
        private async Task ProcessEvent(string eventName, string message, string queueName = null)
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                bool isSubscribedForEvent = false;
                bool isEventSubscribedWithQueue = !string.IsNullOrEmpty(queueName);

                if (isEventSubscribedWithQueue)
                {
                    isSubscribedForEvent = _subsManager.HasSubscriptionsForEvent(queueName);
                }
                else
                {
                    isSubscribedForEvent = _subsManager.HasSubscriptionsForEvent(eventName);
                }

                if (isSubscribedForEvent)
                {
                    IEnumerable<Subscription> subscriptions;

                    if (isEventSubscribedWithQueue)
                    {
                        subscriptions = _subsManager.GetHandlersForEvent(queueName);
                    }
                    else
                    {
                        subscriptions = _subsManager.GetHandlersForEvent(eventName);
                    }

                    foreach (var subscription in subscriptions)
                    {
                        var eventHandler = scope.ServiceProvider.GetService(subscription.EventHandlerType);

                        if (eventHandler == null) continue;

                        var eventType = _subsManager.GetEventByName(eventName);

                        var @event = JsonConvert.DeserializeObject(message, eventType);

                        var concreteType = typeof(IEventHandler<>).MakeGenericType(eventType);

                        await Task.Yield();
                        await (Task)concreteType.GetMethod("Handle").Invoke(eventHandler, new object[] { @event });

                        _logger.Information("Processed event: {EventName} with handler {eventHandler}", eventName, eventHandler);
                    }
                }
                else
                {
                    _logger.Warning("No subscription for event: {EventName} in queue {queueName}", eventName, queueName);
                }
            }
        }

        /// <summary>
        /// Dispose RabbitMQ channel and subscription manager
        /// </summary>
        public void Dispose()
        {
            if (_consumerChannel != null)
            {
                _consumerChannel.Dispose();
            }

            _subsManager.ClearSubscriptions();
        }

    }
}
