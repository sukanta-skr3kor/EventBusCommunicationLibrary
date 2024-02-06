//*********************************************************************************************
//* File             :   SignalREventBus.cs
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
using Microsoft.AspNet.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Polly;
using Serilog;
using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Sukanta.EventBus.SignalR
{
    /// <summary>
    /// SignalR EventBus
    /// </summary>
    public class SignalREventBus : IEventBus, IDisposable
    {
        private readonly ISignalRConnection _signalRConnection;
        private readonly IEventBusSubscriptionManager _subsManager;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly int _retryCount;
        private readonly string _signalRJunctionName;
        private readonly ILogger _logger;

        /// <summary>
        /// SignalR ConnectionId
        /// </summary>
        public string ConnectionId { get; }

        /// <summary>
        /// IsConnected to SignalR server
        /// </summary>
        public bool IsConnected => _signalRConnection != null && _signalRConnection.IsConnected;

        /// <summary>
        /// SignalR Bus
        /// </summary>
        public CommunicationBus CommunicationMode => CommunicationBus.SignalR;

        /// <summary>
        /// SignalR EventBus
        /// </summary>
        /// <param name="signalRConnection"></param>
        /// <param name="subsManager"></param>
        /// <param name="serviceScopeFactory"></param>
        /// <param name="logger"></param>
        /// <param name="signalRJunctionName"></param>
        /// <param name="retryCount"></param>
        public SignalREventBus(ISignalRConnection signalRConnection, IEventBusSubscriptionManager subsManager, IServiceScopeFactory serviceScopeFactory, ILogger logger, string signalRJunctionName, int retryCount = 5)
        {
            _signalRConnection = signalRConnection ?? throw new ArgumentNullException(nameof(signalRConnection));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _subsManager = subsManager ?? new EventBusSubscriptionManager();
            _signalRJunctionName = signalRJunctionName;
            _retryCount = retryCount;
            _serviceScopeFactory = serviceScopeFactory;
            ConnectionId = _signalRConnection.ConnectionId;
        }

        /// <summary>
        /// Dispose SignalR connection
        /// </summary>
        public void Dispose()
        {
            if (_signalRConnection != null)
            {
                _signalRConnection.Dispose();
            }

            _subsManager.ClearSubscriptions();
        }

        /// <summary>
        ///Publish
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="event"></param>
        public void Publish<T>(T @event) where T : Event
        {
            try
            {
                if (!_signalRConnection.IsConnected)
                {
                    _signalRConnection.TryConnect();
                }

                var policy = Policy.Handle<SocketException>()
                   .WaitAndRetry(_retryCount, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), (ex, time) =>
                   {
                       _logger.Warning(ex, "Couldn't publish event: {EventId} after {Timeout}s ({ExceptionMessage})", @event.Id, $"{time.TotalSeconds:n1}", ex.Message);
                   });

                ///policy will be added once the Exceptions are identified
                var eventName = $"{@event.GetType().Name}";
                var hubName = $"{eventName}Hub";

                var message = JsonConvert.SerializeObject(@event, Formatting.Indented, new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.Auto
                });

                policy.Execute(() =>
                {
                    if (_signalRConnection._hubProxyDetails.TryGetValue(hubName, out IHubProxy hubProxy))
                    {
                        hubProxy.Invoke($"{_signalRJunctionName}", message);
                        _logger.Information($"published event with Id : {@event.Id}");

                    }
                });
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        /// Subscribe
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
                DoInternalSignalRSubscription(eventName);

                _subsManager.AddSubscription<T, TH>();
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        ///Do Internal SignalR Subscription
        /// </summary>
        /// <param name="eventName"></param>
        private void DoInternalSignalRSubscription(string eventName)
        {
            var hubName = $"{eventName}Hub";
            var containsKey = _subsManager.HasSubscriptionsForEvent(hubName);
            if (!containsKey)
            {
                if (!_signalRConnection.IsConnected)
                {
                    _signalRConnection.TryConnect();
                }

                if (_signalRConnection._hubProxyDetails.TryGetValue(hubName, out IHubProxy hubProxy))
                {
                    hubProxy.On($"{_signalRJunctionName.ToLower()}", message =>
                     {
                         ProcessEvent(eventName, message).ConfigureAwait(false);
                     });
                }
            }
        }

        /// <summary>
        /// Process Event
        /// </summary>
        /// <param name="eventName"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        private async Task ProcessEvent(string eventName, string message)
        {
            try
            {
                using (var scope = _serviceScopeFactory.CreateScope())
                {
                    if (_subsManager.HasSubscriptionsForEvent(eventName))
                    {
                        var subscriptions = _subsManager.GetHandlersForEvent(eventName);

                        foreach (var subscription in subscriptions)
                        {
                            var handler = scope.ServiceProvider.GetService(subscription.EventHandlerType);

                            if (handler == null) continue;

                            var eventType = _subsManager.GetEventByName(eventName);

                            var @event = JsonConvert.DeserializeObject(message, eventType);

                            var concreteType = typeof(IEventHandler<>).MakeGenericType(eventType);

                            await Task.Yield();
                            await (Task)concreteType.GetMethod("Handle").Invoke(handler, new object[] { @event });

                            _logger.Information("Processed event: {EventName}", eventName);
                        }
                    }
                    else
                    {
                        _logger.Warning("No subscription for event: {EventName}", eventName);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, ex.Message);
            }
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
            var eventName = typeof(T).Name;

            _subsManager.RemoveSubscription<T, TH>(queueName);

            _logger.Information($"Unsubscribed event {eventName}", eventName);
        }

        /// <summary>
        /// Publish
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="event"></param>
        /// <param name="queueName"></param>
        public void Publish<T>(T @event, string queueName) where T : Event
        {
            try
            {
                if (!_signalRConnection.IsConnected)
                {
                    _signalRConnection.TryConnect();
                }

                var policy = Policy.Handle<SocketException>()
                   .WaitAndRetry(_retryCount, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), (ex, time) =>
                   {
                       _logger.Warning(ex, "Couldn't publish event: {EventId} after {Timeout}s ({ExceptionMessage})", @event.Id, $"{time.TotalSeconds:n1}", ex.Message);
                   });

                ///policy will be added once the Exceptions are identified
                var eventName = $"{@event.GetType().Name}";
                var hubName = $"{eventName}Hub";

                var message = JsonConvert.SerializeObject(@event, Formatting.Indented, new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.Auto
                });

                policy.Execute(() =>
                {
                    if (_signalRConnection._hubProxyDetails.TryGetValue(hubName, out IHubProxy hubProxy))
                    {
                        hubProxy.Invoke($"{queueName}", message);
                        _logger.Information($"published event with Id : {@event.Id}");

                    }
                });
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        /// Subscribe
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
                var eventName = _subsManager.GetEventKey<T>();
                DoInternalSignalRSubscription(eventName, queueName);

                _subsManager.AddSubscription<T, TH>();
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        /// DoInternalSignalRSubscription with queuename
        /// </summary>
        /// <param name="eventName"></param>
        /// <param name="queueName"></param>
        private void DoInternalSignalRSubscription(string eventName, string queueName)
        {
            var hubName = $"{eventName}Hub";
            var containsKey = _subsManager.HasSubscriptionsForEvent(hubName);
            if (!containsKey)
            {
                if (!_signalRConnection.IsConnected)
                {
                    _signalRConnection.TryConnect();
                }

                if (_signalRConnection._hubProxyDetails.TryGetValue(hubName, out IHubProxy hubProxy))
                {
                    hubProxy.On($"{queueName.ToLower()}", message =>
                    {
                        ProcessEvent(eventName, message).ConfigureAwait(false);
                    });
                }
            }
        }

    }
}
