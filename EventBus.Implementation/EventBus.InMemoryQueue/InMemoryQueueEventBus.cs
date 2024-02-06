//*********************************************************************************************
//* File             :   InMemoryQueueEventBus.cs
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
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Sukanta.EventBus.InMemoryQueue
{
    /// <summary>
    /// InMemoryQueueEventBus implemented a BlockingCollection type
    /// </summary>
    public class InMemoryQueueEventBus : IEventBus, IDisposable
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger _logger;
        private readonly IResilientPolicy _resilientPolicy;
        private readonly IEventBusSubscriptionManager _subsManager;
        private bool consumerStarted = false;
        private readonly int QueueSize = 100000;

        /// <summary>
        /// An unique GUID
        /// </summary>
        public string ConnectionId { get; }

        /// <summary>
        /// Is queeue created ?
        /// </summary>
        public bool IsConnected => MessageQueue != null;

        /// <summary>
        /// InMemory Queue type
        /// </summary>
        public CommunicationBus CommunicationMode => CommunicationBus.InMemory;

        /// <summary>
        /// Inmemory Queue of BlockingCollection type(thread safe)
        /// </summary>
        private static BlockingCollection<Event> MessageQueue { get; set; }

        /// <summary>
        /// List of MessageQueues
        /// </summary>
        private static Dictionary<string, InMemoryQueue<Event>> MessageQueues { get; set; }

        private int _messageEnqueueCount;
        /// <summary>
        /// Message EnqueueCount
        /// </summary>
        public int MessageEnqueueCount
        {
            get => _messageEnqueueCount;
            set => _messageEnqueueCount = value;
        }

        private int _enqueueFailureCount;
        /// <summary>
        /// EnqueueFailureCount
        /// </summary>
        public int EnqueueFailureCount
        {
            get => _enqueueFailureCount;
            set => _enqueueFailureCount = value;
        }

        /// <summary>
        /// InMemoryQueueEventBus
        /// </summary>
        /// <param name="queueSize"></param>
        /// <param name="subsManager"></param>
        /// <param name="serviceScopeFactory"></param>
        /// <param name="logger"></param>
        /// <param name="resiliencyPolicy"></param>
        public InMemoryQueueEventBus(int queueSize, IEventBusSubscriptionManager subsManager, IServiceScopeFactory serviceScopeFactory,
                                        ILogger logger, IResilientPolicy resiliencyPolicy = null)
        {
            QueueSize = queueSize;
            MessageQueue = new BlockingCollection<Event>(QueueSize);
            _serviceScopeFactory = serviceScopeFactory;
            _subsManager = subsManager ?? new EventBusSubscriptionManager();
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _resilientPolicy = resiliencyPolicy ?? new CommonResilientPolicy(_logger);
            ConnectionId = Guid.NewGuid().ToString();
            MessageQueues = new Dictionary<string, InMemoryQueue<Event>>();
        }

        /// <summary>
        /// Publish
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="event"></param>
        public void Publish<T>(T @event) where T : Event
        {
            //Add the device message to queue.
            Interlocked.Increment(ref _messageEnqueueCount);

            if (!MessageQueue.TryAdd(@event))
            {
                Interlocked.Increment(ref _enqueueFailureCount);

                if (EnqueueFailureCount % QueueSize == 0)
                {
                    _logger.Information($"Application internal message queue count exceed the limit set {MessageEnqueueCount}");
                }
            }

            _logger.Information("Data with id {Id} added to queue", @event.Id);
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
            var eventName = typeof(T).Name;

            try
            {
                _logger.Information("Subscribing to event {EventName} with {EventHandler}", eventName, typeof(TH).Name);

                _subsManager.AddSubscription<T, TH>();
            }
            catch (Exception)
            {
                _logger.Warning("{eventName} already subscribed.", eventName);
            }

            if (!consumerStarted)
            {
                //Start queue consumption
                StartMessageConsume().ConfigureAwait(false);
                consumerStarted = true;
            }
        }

        /// <summary>
        /// Publish
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="event"></param>
        /// <param name="queueName"></param>
        public void Publish<T>(T @event, string queueName) where T : Event
        {
            //Get the queue
            var messageQueue = GetQueue(queueName);

            //Add the device message to queue.
            messageQueue.TryAdd(@event);

            _logger.Information("Data with id {Id} added to queue {queueName}", @event.Id, queueName);
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
            var eventName = typeof(T).Name;

            try
            {
                _logger.Information("Subscribing to event {EventName} with {EventHandler}", eventName, typeof(TH).Name);

                _subsManager.AddSubscription<T, TH>();
            }
            catch (Exception)
            {
                _logger.Warning("{eventName} already subscribed.", eventName);
            }
            //Start queue consumption
            StartMessageConsume(queueName).ConfigureAwait(false);
        }

        /// <summary>
        /// Process the Event by the respective handler
        /// </summary>
        /// <param name="eventData"></param>
        /// <returns></returns>
        private async Task ProcessEvent(Event eventData)
        {
            var eventName = eventData.Name;
            string message = JsonConvert.SerializeObject(eventData);

            using (var scope = _serviceScopeFactory.CreateScope())
            {
                if (_subsManager.HasSubscriptionsForEvent(eventName))
                {
                    var subscriptions = _subsManager.GetHandlersForEvent(eventName);

                    foreach (var subscription in subscriptions)
                    {
                        var eventHandler = scope.ServiceProvider.GetService(subscription.EventHandlerType);

                        if (eventHandler == null) continue;

                        var eventType = _subsManager.GetEventByName(eventName);

                        var @event = JsonConvert.DeserializeObject(message, eventType);

                        var concreteType = typeof(IEventHandler<>).MakeGenericType(eventType);

                        await Task.Yield();
                        await (Task)concreteType.GetMethod("Handle").Invoke(eventHandler, new object[] { @event });

                        _logger.Information("Processed event with Id: {Id}", eventData.Id);

                    }
                }
                else
                {
                    _logger.Warning("No subscription for event: {EventName}", eventName);
                }
            }
        }

        /// <summary>
        /// List of internal queues
        /// </summary>
        /// <param name="queueName"></param>
        /// <returns></returns>
        private InMemoryQueue<Event> GetQueue(string queueName)
        {
            if (!MessageQueues.ContainsKey(queueName))
            {
                InMemoryQueue<Event> inMemoryQueue = new InMemoryQueue<Event>(queueName, QueueSize);

                MessageQueues.Add(queueName, inMemoryQueue);
            }

            return MessageQueues.FirstOrDefault(x => x.Key == queueName).Value;
        }

        /// <summary>
        /// StartMessageConsume from internal queue
        /// </summary>
        private async Task StartMessageConsume()
        {
            try
            {
                do
                {
                    //Take from queue
                    bool gotEvent = MessageQueue.TryTake(out Event eventData);

                    if (gotEvent)
                    {
                        //Process
                        await ProcessEvent(eventData).ConfigureAwait(false);
                    }

                    await Task.Delay(1);
                }
                while (true);
            }
            catch (Exception exp)
            {
                _logger.Error(exp.Message);
            }
        }


        /// <summary>
        ///  StartMessageConsume from defined internal queue
        /// </summary>
        /// <param name="queueName"></param>
        /// <returns></returns>
        private async Task StartMessageConsume(string queueName)
        {
            try
            {
                do
                {
                    //Get the queue
                    var messageQueue = GetQueue(queueName);

                    //Take from queue
                    bool gotEvent = messageQueue.TryTake(out Event eventData);

                    if (gotEvent)
                    {
                        //Process
                        await ProcessEvent(eventData).ConfigureAwait(false);
                    }

                    await Task.Delay(1);
                }
                while (true);
            }
            catch (Exception exp)
            {
                _logger.Error(exp.Message);
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
            var eventName = _subsManager.GetEventKey<T>();
            _subsManager.RemoveSubscription<T, TH>(queueName);

            if (string.IsNullOrEmpty(queueName))
                _logger.Information("Unsubscribed from event {EventName}", eventName);
            else
                _logger.Information("Unsubscribed from event {EventName} from queue {queueName}", eventName, queueName);
        }

        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose()
        {
            MessageQueue.Dispose();
            _subsManager.ClearSubscriptions();
        }

    }
}
