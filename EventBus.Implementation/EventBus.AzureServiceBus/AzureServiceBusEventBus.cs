//*********************************************************************************************
//* File             :   AzureServiceBusEventBus.cs
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

namespace Sukanta.EventBus.AzureServiceBus
{
    using Sukanta.EventBus.Abstraction.Bus;
    using Sukanta.EventBus.Abstraction.Common;
    using Sukanta.EventBus.Abstraction.Events;
    using Sukanta.EventBus.Abstraction.SubscriptionManager;
    using Microsoft.Azure.ServiceBus;
    using Microsoft.Extensions.DependencyInjection;
    using Newtonsoft.Json;
    using Serilog;
    using System;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// AzureServiceBus EventBus implementation
    /// </summary>
    public class AzureServiceBusEventBus : IEventBus
    {
        private readonly IAzureServiceBusConnection _serviceBusConnection;
        private readonly ILogger _logger;
        private readonly IEventBusSubscriptionManager _subsManager;
        private readonly SubscriptionClient _subscriptionClient;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private const string EVENT_SUFFIX = "Event";

        /// <summary>
        /// ServiceBus ConnectionId
        /// </summary>
        public string ConnectionId => _subscriptionClient.ClientId;

        /// <summary>
        /// Is Connected ?
        /// </summary>
        public bool IsConnected => !_subscriptionClient.IsClosedOrClosing;

        /// <summary>
        ///AzureServiceBus
        /// </summary>
        public CommunicationBus CommunicationMode => CommunicationBus.AzureServiceBus;

        /// <summary>
        /// AzureServiceBus EventBus
        /// </summary>
        /// <param name="serviceBusConnection"></param>
        /// <param name="logger"></param>
        /// <param name="subsManager"></param>
        /// <param name="subscriptionClientName"></param>
        /// <param name="serviceScopeFactory"></param>
        public AzureServiceBusEventBus(IAzureServiceBusConnection serviceBusConnection,
            ILogger logger, IEventBusSubscriptionManager subsManager, string subscriptionClientName,
            IServiceScopeFactory serviceScopeFactory)
        {
            _serviceBusConnection = serviceBusConnection;
            _serviceScopeFactory = serviceScopeFactory;
            _subsManager = subsManager ?? new EventBusSubscriptionManager();
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _subscriptionClient = new SubscriptionClient(serviceBusConnection.AzureServiceBusConnectionString, subscriptionClientName);

            RemoveRule();
            RegisterSubscriptionClientHandler();
        }

        /// <summary>
        /// Publish an event to AzureServiceBus
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="event"></param>
        public void Publish<T>(T @event) where T : Event
        {
            try
            {
                var eventName = @event.GetType().Name.Replace(EVENT_SUFFIX, "");
                var jsonMessage = JsonConvert.SerializeObject(@event);
                var body = Encoding.UTF8.GetBytes(jsonMessage);

                var message = new Message
                {
                    MessageId = Guid.NewGuid().ToString(),
                    Body = body,
                    Label = eventName,
                };

                ITopicClient serviceBusTopicClient = _serviceBusConnection.CreateTopicClient();

                serviceBusTopicClient.SendAsync(message).GetAwaiter().GetResult();

                _logger.Information("Published event {EventName} with {EventId}", eventName, @event.Id);
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        ///Subscribe to an event
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TH"></typeparam>
        public void Subscribe<T, TH>()
            where T : Event
            where TH : IEventHandler<T>
        {
            try
            {
                var eventName = typeof(T).Name.Replace(EVENT_SUFFIX, "");

                try
                {
                    _subscriptionClient.AddRuleAsync(new RuleDescription
                    {
                        Filter = new CorrelationFilter { Label = eventName },
                        Name = eventName
                    }).GetAwaiter().GetResult();
                }
                catch (ServiceBusException)
                {
                    _logger.Warning("Messaging entity {eventName} already exists.", eventName);
                }

                _logger.Information("Subscribing to event {EventName} with {EventHandler}", eventName, typeof(TH).Name);

                _subsManager.AddSubscription<T, TH>();
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        /// Unsubscribe to an event
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TH"></typeparam>
        /// <param name="queueName"></param>
        public void Unsubscribe<T, TH>(string queueName = null)
            where T : Event
            where TH : IEventHandler<T>
        {
            var eventName = typeof(T).Name.Replace(EVENT_SUFFIX, "");

            try
            {
                _subscriptionClient
                 .RemoveRuleAsync(eventName)
                 .GetAwaiter()
                 .GetResult();
            }
            catch (MessagingEntityNotFoundException)
            {
                _logger.Warning($"The messaging entity {eventName} Could not be found.", eventName);
            }

            _logger.Information($"Unsubscribing event {eventName}", eventName);

            _subsManager.RemoveSubscription<T, TH>();
        }

        /// <summary>
        /// clear all subscriptions
        /// </summary>
        public void Dispose()
        {
            _subsManager.ClearSubscriptions();
        }

        /// <summary>
        /// Register SubscriptionClient MessageHandler
        /// </summary>
        private void RegisterSubscriptionClientHandler()
        {
            _subscriptionClient.RegisterMessageHandler(
                async (message, token) =>
                {
                    var eventName = $"{message.Label}{EVENT_SUFFIX}";
                    var messageData = Encoding.UTF8.GetString(message.Body);

                    // Complete the message so that it is not received again.
                    if (await ProcessEvent(eventName, messageData))
                    {
                        await _subscriptionClient.CompleteAsync(message.SystemProperties.LockToken);
                    }
                },
                new MessageHandlerOptions(ReceivedExceptionHandler) { MaxConcurrentCalls = 10, AutoComplete = false });
        }

        /// <summary>
        /// ExceptionReceivedHandler
        /// </summary>
        /// <param name="exceptionReceivedEventArgs"></param>
        /// <returns></returns>
        private Task ReceivedExceptionHandler(ExceptionReceivedEventArgs exceptionReceivedEventArgs)
        {
            var ex = exceptionReceivedEventArgs.Exception;
            var context = exceptionReceivedEventArgs.ExceptionReceivedContext;

            _logger.Error(ex, "ERROR handling message: {ExceptionMessage} - Context: {@ExceptionContext}", ex.Message, context);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Process an Event
        /// </summary>
        /// <param name="eventName"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        private async Task<bool> ProcessEvent(string eventName, string message)
        {
            var isProcessed = false;

            if (_subsManager.HasSubscriptionsForEvent(eventName))
            {
                using (var scope = _serviceScopeFactory.CreateScope())
                {
                    var subscriptions = _subsManager.GetHandlersForEvent(eventName);

                    foreach (var subscription in subscriptions)
                    {
                        var handler = scope.ServiceProvider.GetService(subscription.EventHandlerType);
                        if (handler == null) continue;

                        var eventType = _subsManager.GetEventByName(eventName);
                        var eventData = JsonConvert.DeserializeObject(message, eventType);

                        var concreteType = typeof(IEventHandler<>).MakeGenericType(eventType);

                        await (Task)concreteType.GetMethod("Handle").Invoke(handler, new object[] { eventData });

                        _logger.Information("Processed event: {EventName}", eventName);
                    }
                }
                isProcessed = true;
            }
            return isProcessed;
        }

        /// <summary>
        ///Remove Rule
        /// </summary>
        private void RemoveRule()
        {
            try
            {
                _subscriptionClient
                 .RemoveRuleAsync(RuleDescription.DefaultRuleName)
                 .GetAwaiter()
                 .GetResult();
            }
            catch (MessagingEntityNotFoundException)
            {
                _logger.Warning("Messaging entity {DefaultRuleName} not found.", RuleDescription.DefaultRuleName);
            }
        }

        /// <summary>
        /// Publish to any particular queue not support in AzureServiceBus, it will publish default
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="event"></param>
        /// <param name="queueName"></param>
        public void Publish<T>(T @event, string queueName) where T : Event
        {
            try
            {
                var eventName = @event.GetType().Name.Replace(EVENT_SUFFIX, "");
                var jsonMessage = JsonConvert.SerializeObject(@event);
                var body = Encoding.UTF8.GetBytes(jsonMessage);

                var message = new Microsoft.Azure.ServiceBus.Message
                {
                    MessageId = Guid.NewGuid().ToString(),
                    Body = body,
                    Label = eventName,
                };

                IQueueClient serviceBusQueueClient = _serviceBusConnection.CreateQueueClient(queueName);

                serviceBusQueueClient.SendAsync(message).GetAwaiter().GetResult();

                _logger.Information("Published event {EventName} with {EventId} to queue {queueName}", eventName, @event.Id, queueName);
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        /// Subscribe to specific queue not supported, default subscribe will be used.
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
                var eventName = typeof(T).Name.Replace(EVENT_SUFFIX, "");

                try
                {
                    IQueueClient serviceBusQueueClient = _serviceBusConnection.CreateQueueClient(queueName);

                    RegisterQueueClientHandler(serviceBusQueueClient);
                }
                catch (ServiceBusException)
                {
                    _logger.Warning("Messaging queue/entity {queueName} already exists.", queueName);
                }

                _logger.Information("Subscribing to event {EventName} with {EventHandler} for queue {queueName}", eventName, typeof(TH).Name, queueName);

                _subsManager.AddSubscription<T, TH>();
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        /// Register QueueClient Handler
        /// </summary>
        /// <param name="serviceBusQueueClient"></param>
        private void RegisterQueueClientHandler(IQueueClient serviceBusQueueClient)
        {
            serviceBusQueueClient.RegisterMessageHandler(
                async (message, token) =>
                {
                    var eventName = $"{message.Label}{EVENT_SUFFIX}";
                    var messageData = Encoding.UTF8.GetString(message.Body);

                    // Complete the message so that it is not received again.
                    if (await ProcessEvent(eventName, messageData))
                    {
                        await serviceBusQueueClient.CompleteAsync(message.SystemProperties.LockToken);
                    }
                },
                new MessageHandlerOptions(ReceivedExceptionHandler) { MaxConcurrentCalls = 10, AutoComplete = false });
        }

    }
}
