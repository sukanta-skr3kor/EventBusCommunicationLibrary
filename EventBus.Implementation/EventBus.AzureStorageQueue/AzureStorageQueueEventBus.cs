//*********************************************************************************************
//* File             :   AzureStorageQueueEventBus.cs
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
namespace Sukanta.EventBus.AzureStorageQueue
{
    using Sukanta.EventBus.Abstraction.Bus;
    using Sukanta.EventBus.Abstraction.Common;
    using Sukanta.EventBus.Abstraction.Events;
    using Sukanta.EventBus.Abstraction.SubscriptionManager;
    using Sukanta.Resiliency;
    using Sukanta.Resiliency.Abstraction;
    using Microsoft.Azure.Storage;
    using Microsoft.Azure.Storage.Queue;
    using Microsoft.Extensions.DependencyInjection;
    using Newtonsoft.Json;
    using Polly;
    using Serilog;
    using System;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// AzureStorageQueue EventBus implementation
    /// </summary>
    public class AzureStorageQueueEventBus : IEventBus, IDisposable
    {
        private readonly IAzureStorageQueueConnection _storageQueueConnection;
        private readonly ILogger _logger;
        private readonly IEventBusSubscriptionManager _subsManager;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IResilientPolicy _resilientPolicy;
        private int _retryCount;

        //Max 64KB message can be sent to azure storage queue in 1 publish
        private const int MAX_MESSAGE_SIZE = 8000;

        /// <summary>
        /// ServiceBus ConnectionId
        /// </summary>
        public string ConnectionId { get; }

        /// <summary>
        /// Is Connected ?
        /// </summary>
        public bool IsConnected => _storageQueueConnection.cloudStorageAccount != null;

        /// <summary>
        ///AzureServiceBus
        /// </summary>
        public CommunicationBus CommunicationMode => CommunicationBus.AzureStorageQueue;

        /// <summary>
        ///Storage Queue name 
        /// </summary>
        public string QueueName { get; set; }

        /// <summary>
        /// AzureStorageQueue EventBus
        /// </summary>
        /// <param name="storageQueueConnection"></param>
        /// <param name="subsManager"></param>
        /// <param name="serviceScopeFactory"></param>
        /// <param name="logger"></param>
        /// <param name="queueName"></param>
        /// <param name="resiliencyPolicy"></param>
        /// <param name="retryCount"></param>
        public AzureStorageQueueEventBus(IAzureStorageQueueConnection storageQueueConnection, IEventBusSubscriptionManager subsManager, IServiceScopeFactory serviceScopeFactory,
                                           ILogger logger, string queueName = null, IResilientPolicy resiliencyPolicy = null, int retryCount = 3)
        {
            _storageQueueConnection = storageQueueConnection;
            _serviceScopeFactory = serviceScopeFactory;
            _subsManager = subsManager ?? new EventBusSubscriptionManager();
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            QueueName = queueName;
            _resilientPolicy = resiliencyPolicy ?? new CommonResilientPolicy(_logger);
            ConnectionId = Guid.NewGuid().ToString();
            _retryCount = retryCount;
        }


        /// <summary>
        /// Publish an event to Azure Storage Queue
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="event"></param>
        public void Publish<T>(T @event) where T : Event
        {
            try
            {
                var eventName = @event.GetType().Name;
                var jsonMessage = JsonConvert.SerializeObject(@event);
                var content = Encoding.UTF8.GetBytes(jsonMessage);
                var messageSize = content.Length;

                if (messageSize > MAX_MESSAGE_SIZE)
                    throw new StorageException("Message size is more than the allowed limit of 64KB.");

                if (messageSize < MAX_MESSAGE_SIZE)
                {
                    CloudQueueMessage publishedMessage = new CloudQueueMessage(content);

                    if (string.IsNullOrEmpty(QueueName))
                    {
                        QueueName = eventName;
                        _logger.Warning("No storage queue defined using default queue {QueueName}", QueueName);
                    }

                    QueueName = QueueName.ToLowerInvariant();

                    var queue = _storageQueueConnection.GetQueue(QueueName);

                    //Using retry policy
                    var policy = Policy.Handle<StorageException>()
                     .WaitAndRetry(_retryCount, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), (ex, time) =>
                     {
                         _logger.Warning(ex, "Couldn't publish event to storage queue {QueueName}: {EventId} after {Timeout}s ({ExceptionMessage})", QueueName, @event.Id, $"{time.TotalSeconds:n1}", ex.Message);
                     });

                    policy.Execute(() =>
                       {
                           if (queue.CreateIfNotExists())
                           {
                               _logger.Information("Created storage queue {QueueName}", QueueName);
                           }

                           //Add the message to queue
                           queue?.AddMessageAsync(publishedMessage).ConfigureAwait(false);

                           _logger.Information("Published event {EventName} id {EventId} to queue {QueueName}", eventName, @event.Id, QueueName);
                       });
                }
                else
                {
                    _logger.Error("Message size {messageSize} bytes exceeds allowed limit for Event {EventName} id {EventId} to queue {queueName}", messageSize, eventName, @event.Id);
                }
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        /// Publish to specific storage Queue
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="event"></param>
        /// <param name="queueName"></param>
        public void Publish<T>(T @event, string queueName) where T : Event
        {
            try
            {
                var eventName = @event.GetType().Name;
                var jsonMessage = JsonConvert.SerializeObject(@event);
                var content = Encoding.UTF8.GetBytes(jsonMessage);
                var messageSize = content.Length;

                if (messageSize > MAX_MESSAGE_SIZE)
                    throw new StorageException("Message size is more than the allowed limit of 64KB.");

                if (messageSize < MAX_MESSAGE_SIZE)
                {
                    CloudQueueMessage publishedMessage = new CloudQueueMessage(content);

                    queueName = queueName.ToLowerInvariant();

                    var queue = _storageQueueConnection.GetQueue(queueName);

                    //Using retry policy
                    var policy = Policy.Handle<StorageException>()
                     .WaitAndRetry(_retryCount, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), (ex, time) =>
                     {
                         _logger.Warning(ex, "Couldn't publish event to storage queue {queueName}: {EventId} after {Timeout}s ({ExceptionMessage})", queueName, @event.Id, $"{time.TotalSeconds:n1}", ex.Message);
                     });

                    policy.Execute(() =>
                    {
                        if (queue.CreateIfNotExists())
                        {
                            _logger.Information("Created storage queue {queueName}", queueName);
                        }

                        //Add the message to queue
                        queue?.AddMessageAsync(publishedMessage).ConfigureAwait(false);

                        _logger.Information("Published event {EventName} id {EventId} to queue {queueName}", eventName, @event.Id, queueName);
                    });
                }
                else
                {
                    _logger.Error("Message size {messageSize} bytes exceeds allowed limit for Event {EventName} id {EventId} to queue {queueName}", messageSize, eventName, @event.Id);
                }
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

                if (string.IsNullOrEmpty(QueueName))
                    QueueName = eventName.ToLowerInvariant();

                //Start consuming the messages from the storagequeue
                StartConsumeAsync(QueueName).ConfigureAwait(false);
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        /// Subscribe/Consume from a spcific storage queue
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TH"></typeparam>
        /// <param name="queueName"></param>
        public void Subscribe<T, TH>(string queueName) where T : Event where TH : IEventHandler<T>
        {
            try
            {
                var eventName = typeof(T).Name;
                queueName = queueName.ToLowerInvariant();

                try
                {
                    _logger.Information("Subscribing to event {EventName} with {EventHandler} from storage queue", eventName, typeof(TH).Name, queueName);

                    _subsManager.AddSubscription<T, TH>();
                }
                catch (Exception)
                {
                    _logger.Warning("{eventName} already subscribed.", eventName);
                }

                //Start consuming the messages from the storagequeue
                StartConsumeAsync(queueName).ConfigureAwait(false);
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        /// Start consume messages from the storage queue
        /// </summary>
        /// <param name="queueName"></param>
        /// <returns></returns>
        private async Task StartConsumeAsync(string queueName)
        {
            _logger.Information("Starting message consume from StorageQueue {queueName}", queueName);

            string eventName = string.Empty;

            do
            {
                try
                {
                    var queue = _storageQueueConnection.GetQueue(queueName);

                    if (queue != null)
                    {
                        CloudQueueMessage retrievedMessage = await queue.GetMessageAsync().ConfigureAwait(false);

                        if (retrievedMessage != null)
                        {
                            var @event = JsonConvert.DeserializeObject<Event>(retrievedMessage.AsString);
                            eventName = @event.Name;

                            //Process message
                            bool isProcessed = await ProcessEvent(eventName, retrievedMessage.AsString).ConfigureAwait(false);

                            //If processed delete from cloud queue
                            if (isProcessed)
                            {
                                await queue.DeleteMessageAsync(retrievedMessage).ConfigureAwait(false);
                            }
                        }
                    }
                }
                catch (Exception exp)
                {
                    _logger.Warning(exp, "Error processing event {eventName}", eventName);
                }

                //Delay the loop for 10ms
                await Task.Delay(10);

            } while (true);
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
            var eventName = typeof(T).Name;

            _subsManager.RemoveSubscription<T, TH>(queueName);

            if (string.IsNullOrEmpty(queueName))
                _logger.Information("Unsubscribed from event {EventName}", eventName);
            else
                _logger.Information("Unsubscribed from event {EventName} from queue {queueName}", eventName, queueName);
        }

        /// <summary>
        /// Process an Event
        /// </summary>
        /// <param name="eventName"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        private async Task<bool> ProcessEvent(string eventName, string message)
        {
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

                        await Task.Yield();
                        await (Task)concreteType.GetMethod("Handle").Invoke(handler, new object[] { eventData });

                        _logger.Information("Processed event {eventName}", eventName);

                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// clear all subscriptions
        /// </summary>
        public void Dispose()
        {
            _subsManager.ClearSubscriptions();
        }

    }
}
