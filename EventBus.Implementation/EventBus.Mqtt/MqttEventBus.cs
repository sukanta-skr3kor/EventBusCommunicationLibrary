//*********************************************************************************************
//* File             :   MqttEventBus.cs
//* Author           :   Rout, Sukanta 
//* Date             :   22/8/2023
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
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Exceptions;
using Newtonsoft.Json;
using Polly;
using Serilog;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using static Sukanta.EventBus.Abstraction.SubscriptionManager.EventBusSubscriptionManager;

namespace Sukanta.EventBus.Mqtt
{
    /// <summary>
    /// Mqtt EventBus Implementation
    /// </summary>
    public class MqttEventBus : IEventBus, IDisposable
    {
        private readonly IMqttConnection _mqttConnection;
        private readonly IEventBusSubscriptionManager _subsManager;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IResilientPolicy _resilientPolicy;
        private readonly int _retryCount;
        private readonly ILogger _logger;

        /// <summary>
        /// Unique ConnectionId
        /// </summary>
        public string ConnectionId { get; }

        /// <summary>
        /// Is connected to messaging bus
        /// </summary>
        public bool IsConnected => _mqttConnection != null && _mqttConnection.IsConnected;

        /// <summary>
        /// CommunicationBus is Mqtt
        /// </summary>
        public CommunicationBus CommunicationMode => CommunicationBus.Mqtt;

        /// <summary>
        /// Mqtt EventBus
        /// </summary>
        /// <param name="mqttConnection"></param>
        /// <param name="subsManager"></param>
        /// <param name="serviceScopeFactory"></param>
        /// <param name="logger"></param>
        /// <param name="queueName"></param>
        /// <param name="resilientPolicy"></param>
        /// <param name="retryCount"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public MqttEventBus(IMqttConnection mqttConnection, IEventBusSubscriptionManager subsManager, IServiceScopeFactory serviceScopeFactory,
            ILogger logger, string queueName = null, IResilientPolicy resilientPolicy = null, int retryCount = 3)
        {
            _mqttConnection = mqttConnection ?? throw new ArgumentNullException(nameof(mqttConnection));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _subsManager = subsManager ?? new EventBusSubscriptionManager();

            _retryCount = retryCount;
            _serviceScopeFactory = serviceScopeFactory;
            ConnectionId = Guid.NewGuid().ToString();
            _resilientPolicy = resilientPolicy ?? new CommonResilientPolicy(_logger, _retryCount);
        }


        /// <summary>
        /// Publish an event to the Mqtt queue
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="event"></param>
        public void Publish<T>(T @event) where T : Event
        {
            try
            {
                if (!_mqttConnection.IsConnected)
                {
                    _mqttConnection.TryConnect();
                }

                if (_mqttConnection.IsConnected)
                {
                    var topic = @event.GetType().Name;
                    string data = JsonConvert.SerializeObject(@event);

                    //Using retry policy
                    var policy = Policy.Handle<MqttCommunicationException>()
                        .Or<MqttCommunicationTimedOutException>()
                     .Or<SocketException>()
                     .WaitAndRetry(_retryCount, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), (ex, time) =>
                     {
                         _logger.Warning(ex, "Could not publish event: {EventId} after {Timeout}s ({ExceptionMessage})", @event.Id, $"{time.TotalSeconds:n1}", ex.Message);
                     });

                    policy.Execute(() =>
                {
                    var applicationMessage = new MqttApplicationMessageBuilder()
                        .WithTopic(topic)
                        .WithPayload(data)
                        .Build();

                    _mqttConnection.mqttClient.PublishAsync(applicationMessage, CancellationToken.None);

                });
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
        /// Publish to an specific topic
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="event"></param>
        /// <param name="topic"></param>
        public void Publish<T>(T @event, string topic) where T : Event
        {
            if (topic is null)
            {
                throw new ArgumentNullException(nameof(topic));
            }

            try
            {
                if (!_mqttConnection.IsConnected)
                {
                    _mqttConnection.TryConnect();
                }

                if (_mqttConnection.IsConnected)
                {
                    string data = JsonConvert.SerializeObject(@event);

                    //Using retry policy
                    var policy = Policy.Handle<MqttCommunicationException>()
                     .Or<SocketException>()
                     .WaitAndRetry(_retryCount, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), (ex, time) =>
                     {
                         _logger.Warning(ex, "Could not publish event: {EventId} to queue {queueName} after {Timeout}s ({ExceptionMessage})", @event.Id, $"{time.TotalSeconds:n1}", ex.Message);
                     });

                    policy.Execute(async () =>
                    {
                        await _mqttConnection.mqttClient.PublishStringAsync(topic, data);
                    });


                    _logger.Information("Published event with id {EventId} to topic {topic}", @event.Id, topic);
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
        /// Subscribe from a specific topic
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TH"></typeparam>
        /// <param name="topic"></param>
        public void Subscribe<T, TH>(string topic)
            where T : Event
            where TH : IEventHandler<T>
        {
            try
            {
                var eventName = _subsManager.GetEventKey<T>();

                try
                {
                    _logger.Information("Subscribing to event {EventName} with {EventHandler}", eventName, typeof(TH).Name);

                    //Add to subscription
                    _subsManager.AddSubscription<T, TH>(topic);
                }
                catch (Exception)
                {
                    _logger.Warning("{eventName} already subscribed.", eventName);
                }

                //Start Mqtt Subscription
                StartSubscribe<T>(eventName);
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

                //Start Mqtt Subscription
                StartSubscribe<T>(eventName);
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        /// Start Subscribe to the topic
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="topic"></param>
        public void StartSubscribe<T>(string topic) where T : Event
        {
            if (!_mqttConnection.IsConnected)
            {
                _mqttConnection.TryConnect();
            }

            if (_mqttConnection.IsConnected)
            {
                _mqttConnection.mqttClient.ApplicationMessageReceivedAsync += MqttClient_ApplicationMessageReceivedAsync;
                var mqttSubscribeOptions = _mqttConnection.mqttFactory.CreateSubscribeOptionsBuilder()
                  .WithTopicFilter(
                      x =>
                      {
                          x.WithTopic(topic);
                      })
                  .Build();

                //Using retry policy
                var policy = Policy.Handle<MqttCommunicationException>()
                 .Or<SocketException>()
                 .WaitAndRetry(_retryCount, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), (ex, time) =>
                 {
                     _logger.Error(ex, "Could not subscribe event: {EventId} after {Timeout}s ({ExceptionMessage})", $"{time.TotalSeconds:n1}", ex.Message);
                 });

                policy.Execute(async () =>
                {
                    await _mqttConnection.mqttClient.SubscribeAsync(mqttSubscribeOptions, CancellationToken.None);
                });
            }
            else
            {
                _logger.Error("Connection with server broken, couldn't subscribe to events.");
            }
        }

        /// <summary>
        /// Process the event received
        /// </summary>
        /// <param name="arg"></param>
        /// <returns></returns>
        private async Task MqttClient_ApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs arg)
        {
            await ProcessEvent(arg.ApplicationMessage.Topic, arg.ApplicationMessage.ConvertPayloadToString());
        }

        /// <summary>
        /// Process the Event by the respective handler
        /// </summary>
        /// <param name="eventName"></param>
        /// <param name="message"></param>
        /// <param name="topic"></param>
        /// <returns></returns>
        private async Task ProcessEvent(string eventName, string message, string topic = null)
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                bool isSubscribedForEvent = false;
                bool isEventSubscribedWithQueue = !string.IsNullOrEmpty(topic);

                if (isEventSubscribedWithQueue)
                {
                    isSubscribedForEvent = _subsManager.HasSubscriptionsForEvent(topic);
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
                        subscriptions = _subsManager.GetHandlersForEvent(topic);
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
                    _logger.Warning("No subscription for event: {EventName} in topic {topic}", eventName, topic);
                }
            }
        }

        /// <summary>
        /// Unsubscribe events from subscription handler
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TH"></typeparam>
        /// <param name="topicName"></param>
        public void Unsubscribe<T, TH>(string topicName = null)
            where T : Event
            where TH : IEventHandler<T>
        {
            var eventName = _subsManager.GetEventKey<T>();
            _subsManager.RemoveSubscription<T, TH>(topicName);

            if (string.IsNullOrEmpty(topicName))
            {
                _logger.Information("Unsubscribed from event {EventName}", eventName);
            }
            else
            {
                _logger.Information("Unsubscribed from event {EventName}", eventName);
            }
        }

        /// <summary>
        /// Dispose Mqtt client and subscription manager
        /// </summary>
        public void Dispose()
        {
            _mqttConnection?.mqttClient?.DisconnectAsync();
            _mqttConnection?.Dispose();
            _subsManager?.ClearSubscriptions();
        }

    }
}
