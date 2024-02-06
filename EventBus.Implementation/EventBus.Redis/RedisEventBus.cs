//*********************************************************************************************
//* File             :   RedisEventBus.cs
//* Author           :   Rout, Sukanta 
//* Date             :   31/8/2023
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
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading.Tasks;
using static Sukanta.EventBus.Abstraction.SubscriptionManager.EventBusSubscriptionManager;
using ILogger = Serilog.ILogger;

namespace Sukanta.EventBus.Redis
{
    /// <summary>
    /// Redis EventBus Implementation
    /// </summary>
    public class RedisEventBus : IEventBus, IDisposable
    {
        private readonly IRedisConnection _redisConnection;
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
        public bool IsConnected => _redisConnection != null && _redisConnection.IsConnected();

        /// <summary>
        /// CommunicationBus is Redis
        /// </summary>
        public CommunicationBus CommunicationMode => CommunicationBus.Redis;

        /// <summary>
        /// Redis EventBus
        /// </summary>
        /// <param name="redisConnection"></param>
        /// <param name="subsManager"></param>
        /// <param name="serviceScopeFactory"></param>
        /// <param name="logger"></param>
        /// <param name="queueName"></param>
        /// <param name="resilientPolicy"></param>
        /// <param name="retryCount"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public RedisEventBus(IRedisConnection redisConnection, IEventBusSubscriptionManager subsManager, IServiceScopeFactory serviceScopeFactory,
            ILogger logger, string queueName = null, IResilientPolicy resilientPolicy = null, int retryCount = 3)
        {
            _redisConnection = redisConnection ?? throw new ArgumentNullException(nameof(redisConnection));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _subsManager = subsManager ?? new EventBusSubscriptionManager();

            _retryCount = retryCount;
            _serviceScopeFactory = serviceScopeFactory;
            ConnectionId = Guid.NewGuid().ToString();
            _resilientPolicy = resilientPolicy ?? new RedisResilientPolicy(_logger, _retryCount);
        }


        /// <summary>
        /// Publish an event to the Redis queue
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="event"></param>
        public void Publish<T>(T @event) where T : Event
        {
            try
            {
                if (!_redisConnection.IsConnected())
                {
                    _redisConnection.TryConnect();
                }

                if (_redisConnection.IsConnected())
                {
                    var topic = @event.GetType().Name;
                    string data = JsonConvert.SerializeObject(@event);

                    //Using retry policy
                    var policy = Policy.Handle<RedisConnectionException>()
                        .Or<RedisException>()
                     .Or<SocketException>()
                     .WaitAndRetry(_retryCount, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), (ex, time) =>
                     {
                         _logger.Warning(ex, "Could not publish event: {EventId} after {Timeout}s ({ExceptionMessage})", @event.Id, $"{time.TotalSeconds:n1}", ex.Message);
                     });

                    policy.Execute(() =>
                    {
                        IConnectionMultiplexer connection = _redisConnection.GetConnection();

                        RedisChannel channel = new RedisChannel(topic, RedisChannel.PatternMode.Pattern);

                        ISubscriber subscriber = connection.GetSubscriber();

                        if (connection.IsConnected)
                        {
                            return subscriber.PublishAsync(channel, data);
                        }
                        else
                        {
                            throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Disconnected from eventbus");
                        }
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
                if (!_redisConnection.IsConnected())
                {
                    _redisConnection.TryConnect();
                }

                if (_redisConnection.IsConnected())
                {
                    string data = JsonConvert.SerializeObject(@event);

                    //Using retry policy
                    var policy = Policy.Handle<RedisConnectionException>()
                     .Or<RedisException>()
                     .WaitAndRetry(_retryCount, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), (ex, time) =>
                     {
                         _logger.Warning(ex, "Could not publish event: {EventId} to queue {queueName} after {Timeout}s ({ExceptionMessage})", @event.Id, $"{time.TotalSeconds:n1}", ex.Message);
                     });

                    policy.Execute(() =>
                    {
                        IConnectionMultiplexer connection = _redisConnection.GetConnection();

                        RedisChannel channel = new RedisChannel(topic, RedisChannel.PatternMode.Pattern);

                        ISubscriber subscriber = connection.GetSubscriber();

                        if (connection.IsConnected)
                        {
                            return subscriber.PublishAsync(channel, data);
                        }
                        else
                        {
                            throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Disconnected from eventbus");
                        }
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

                //Start Redis Subscription
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
            if (!_redisConnection.IsConnected())
            {
                _redisConnection.TryConnect();
            }

            if (_redisConnection.IsConnected())
            {
                //Using retry policy
                var policy = Policy.Handle<RedisConnectionException>()
                 .Or<RedisException>()
                 .WaitAndRetry(_retryCount, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), (ex, time) =>
                 {
                     _logger.Error(ex, "Could not subscribe event: {EventId} after {Timeout}s ({ExceptionMessage})", $"{time.TotalSeconds:n1}", ex.Message);
                 });

                policy.Execute(() =>
                {
                    IConnectionMultiplexer connection = _redisConnection.GetConnection();

                    RedisChannel subscribeChannel = new RedisChannel(topic, RedisChannel.PatternMode.Auto);

                    ISubscriber subscriber = connection.GetSubscriber();

                    subscriber.Subscribe(subscribeChannel, async (channel, message) =>
                    {
                        await ProcessEvent(topic, message);
                    });
                });
            }
            else
            {
                _logger.Error("Connection with server broken, couldn't subscribe to events");
            }
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
        /// Dispose Redis client and subscription manager
        /// </summary>
        public void Dispose()
        {
            _redisConnection?.Dispose();
            _subsManager?.ClearSubscriptions();
        }

    }
}
