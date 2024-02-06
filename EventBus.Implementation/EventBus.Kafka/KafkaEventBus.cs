//*********************************************************************************************
//* File             :   KafkaEventBus.cs
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

using Confluent.Kafka;
using Sukanta.EventBus.Abstraction.Bus;
using Sukanta.EventBus.Abstraction.Common;
using Sukanta.EventBus.Abstraction.Events;
using Sukanta.EventBus.Abstraction.SubscriptionManager;
using Sukanta.Resiliency;
using Sukanta.Resiliency.Abstraction;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Polly;
using Serilog;
using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Sukanta.EventBus.Kafka
{
    public class KafkaEventBus : IEventBus, IDisposable
    {
        private readonly IKafkaConnection _kafkaConnection;
        private readonly IEventBusSubscriptionManager _subsManager;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IResilientPolicy _resilientPolicy;
        private readonly int _retryCount;
        private readonly ILogger _logger;

        public string ConnectionId { get; }

        public bool IsConnected => _kafkaConnection.IsConnected;

        public CommunicationBus CommunicationMode => CommunicationBus.Kafka;

        /// <summary>
        /// KafkaEventBus
        /// </summary>
        /// <param name="kafkaConnection"></param>
        /// <param name="subsManager"></param>
        /// <param name="serviceScopeFactory"></param>
        /// <param name="logger"></param>
        /// <param name="topicName"></param>
        /// <param name="resilientPolicy"></param>
        /// <param name="retryCount"></param>
        public KafkaEventBus(IKafkaConnection kafkaConnection, IEventBusSubscriptionManager subsManager, IServiceScopeFactory serviceScopeFactory,
            ILogger logger, string topicName = null, IResilientPolicy resilientPolicy = null, int retryCount = 5)
        {
            _kafkaConnection = kafkaConnection ?? throw new ArgumentNullException(nameof(kafkaConnection));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _subsManager = subsManager ?? new EventBusSubscriptionManager();
            _retryCount = retryCount;
            _serviceScopeFactory = serviceScopeFactory;
            ConnectionId = Guid.NewGuid().ToString();
            _resilientPolicy = resilientPolicy ?? new CommonResilientPolicy(_logger, _retryCount);
        }

        /// <summary>
        /// Publish a message to kafka
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="event"></param>
        public void Publish<T>(T @event) where T : Event
        {
            try
            {
                //Event name
                var topicName = @event.GetType().Name;
                string eventData = JsonConvert.SerializeObject(@event);

                Action<DeliveryReport<Null, string>> handler = x =>
               _logger.Information(!x.Error.IsError ? $"Published message to {x.TopicPartitionOffset}" : $"Message delivery Error: {x.Error.Reason}");

                var producer = _kafkaConnection.GetProducer(topicName);

                //Using retry policy
                var policy = Policy.Handle<KafkaException>()
                 .Or<SocketException>()
                 .WaitAndRetry(_retryCount, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), (ex, time) =>
                 {
                     _logger.Warning(ex, "Could not publish message {EventId} to kafka, after {Timeout}s ({ExceptionMessage})", @event.Id, $"{time.TotalSeconds:n1}", ex.Message);
                 });

                //Publish the event
                policy.Execute(() =>
                {
                    producer.Produce(topicName, new Message<Null, string> { Value = eventData }, handler);
                });
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        /// Publisg to the topic
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="event"></param>
        /// <param name="queueName"></param>
        public void Publish<T>(T @event, string queueName) where T : Event
        {
            try
            {
                //Topic name
                var topicName = queueName;
                string eventData = JsonConvert.SerializeObject(@event);

                Action<DeliveryReport<Null, string>> handler = x =>
               _logger.Information(!x.Error.IsError ? $"Published message to {x.TopicPartitionOffset}" : $"Message delivery Error: {x.Error.Reason}");

                var producer = _kafkaConnection.GetProducer(topicName);

                //Using retry policy
                var policy = Policy.Handle<KafkaException>()
                 .Or<SocketException>()
                 .WaitAndRetry(_retryCount, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), (ex, time) =>
                 {
                     _logger.Warning(ex, "Could not publish message {EventId} to kafka, after {Timeout}s ({ExceptionMessage})", @event.Id, $"{time.TotalSeconds:n1}", ex.Message);
                 });

                //Publish event
                policy.Execute(() =>
                {
                    producer.Produce(topicName, new Message<Null, string> { Value = eventData }, handler);
                });
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        /// Subscribe to a topic
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
                string topicName = eventName;

                try
                {
                    _logger.Information("Subscribing to event {EventName} with {EventHandler} from topic {eventName}", eventName, typeof(TH).Name, eventName);

                    //Add to subscription
                    _subsManager.AddSubscription<T, TH>();
                }
                catch (Exception)
                {
                    _logger.Warning("{eventName} already subscribed.", eventName);
                }

                var consumer = _kafkaConnection.GetConsumer(topicName);

                //Subscribe from Kafka topic
                consumer.Subscribe(topicName);

                //Start kafka topic consumption
                StartKafkaConsumeAsync(consumer, eventName, topicName).ConfigureAwait(false);
            }
            catch
            {
                throw;
            }
        }


        /// <summary>
        /// Subscribe to the given topic
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TH"></typeparam>
        /// <param name="queueName"></param>
        public void Subscribe<T, TH>(string queueName)
            where T : Event
            where TH : IEventHandler<T>
        {
            try
            {
                string topicName = queueName;
                var eventName = _subsManager.GetEventKey<T>();

                try
                {
                    _logger.Information("Subscribing to event {EventName} with {EventHandler} from topic {queueName}", eventName, typeof(TH).Name, queueName);

                    //Add to subscription
                    _subsManager.AddSubscription<T, TH>(queueName);
                }
                catch (Exception)
                {
                    _logger.Warning("{eventName} already subscribed.", eventName);
                }

                var consumer = _kafkaConnection.GetConsumer(topicName);

                //Subscribe topic from Kafka
                consumer.Subscribe(topicName);

                //Start kafka topic consumption
                StartKafkaConsumeAsync(consumer, eventName, topicName).ConfigureAwait(false);
            }
            catch
            {
                throw;
            }
        }


        /// <summary>
        /// Start Consume messages for the topic
        /// </summary>
        /// <param name="consumer"></param>
        /// <param name="eventName"></param>
        /// <param name="topicName"></param>
        /// <returns></returns>
        private async Task StartKafkaConsumeAsync(IConsumer<Null, string> consumer, string eventName, string topicName)
        {
            await Task.Run(() =>
            {
                do
                {
                    try
                    {
                        //consume message for the topic and if no message timeout after 1 millisecond
                        var consumeResult = consumer.Consume(1);

                        if (consumeResult != null && !string.IsNullOrEmpty(consumeResult.Message.Value))
                        {
                            try
                            {
                                if (_subsManager.HasSubscriptionsForEvent(topicName))
                                {
                                    ProcessEvent(consumeResult.Topic, consumeResult.Message.Value, eventName).ConfigureAwait(false);
                                    _logger.Information($"Consumed message {consumeResult.Message.Value} from {consumeResult.TopicPartitionOffset}");
                                }
                                else
                                {
                                    try
                                    {
                                        //Unsubscribe topic from Kafka
                                        consumer.Unsubscribe();
                                    }
                                    catch (KafkaException exp)
                                    {
                                        _logger.Error(exp, exp.Message);
                                    }
                                }
                            }
                            catch (Exception exp)
                            {
                                _logger.Warning(exp, "Error processing event {eventName}", eventName);
                            }
                        }
                    }
                    catch (ConsumeException exp)
                    {
                        consumer.Close();//close the consumer
                        consumer = _kafkaConnection.GetConsumer(topicName);//Create a new consumer

                        _logger.Error($"Error occured while retriving message : {exp.Error.Reason}");
                    }
                    catch (Exception exp)
                    {
                        _logger.Error($"Error occured while retriving message : {exp.Message}");
                    }

                } while (true);
            });
        }


        /// <summary>
        /// Unsubscribe
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
                _logger.Information("Unsubscribed from event {EventName} from topic {queueName}", eventName, queueName);
            }
        }


        /// <summary>
        /// Process Event for a topic
        /// </summary>
        /// <param name="topicName"></param>
        /// <param name="message"></param>
        /// <param name="eventName"></param>
        /// <returns></returns>
        private async Task ProcessEvent(string topicName, string message, string eventName)
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                bool isSubscribedForEvent = _subsManager.HasSubscriptionsForEvent(topicName);

                if (isSubscribedForEvent)
                {
                    var subscriptions = _subsManager.GetHandlersForEvent(topicName);

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
                    _logger.Warning("No subscription for event: {EventName}", eventName);
                }
            }
        }

        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose()
        {
            _subsManager.ClearSubscriptions();
            _kafkaConnection.Dispose();
        }

    }
}
