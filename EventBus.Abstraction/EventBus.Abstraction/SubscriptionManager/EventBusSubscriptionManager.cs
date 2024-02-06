//*********************************************************************************************
//* File             :   EventBusSubscriptionManager.cs
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

//*********************************************************************************************
//* File             :   EventBusSubscriptionManager.cs
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
using Sukanta.EventBus.Abstraction.Events;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Sukanta.EventBus.Abstraction.SubscriptionManager
{
    /// <summary>
    /// Manages all subscriptions
    /// </summary>
    public partial class EventBusSubscriptionManager : IEventBusSubscriptionManager
    {
        /// <summary>
        /// Thread safe concurrent dictionary to store and manage the subscriptions
        /// </summary>
        private readonly ConcurrentDictionary<string, List<Subscription>> _subscriptionHandlers;

        /// <summary>
        /// Event Types list
        /// </summary>
        private readonly List<Type> _eventTypes;

        /// <summary>
        /// EventRemoved event 
        /// </summary>
        public event EventHandler<EventMetadataEventArgs> OnEventRemoved;

        /// <summary>
        /// EventBusSubscriptionManager
        /// </summary>
        public EventBusSubscriptionManager()
        {
            _subscriptionHandlers = new ConcurrentDictionary<string, List<Subscription>>();
            _eventTypes = new List<Type>();
        }

        /// <summary>
        /// Is subscription empty
        /// </summary>
        public bool IsSubscriptionsEmpty => !_subscriptionHandlers.Keys.Any();

        /// <summary>
        /// Clear subscription
        /// </summary>
        public void ClearSubscriptions() => _subscriptionHandlers.Clear();

        /// <summary>
        /// If subscription for the queue message is empty
        /// </summary>
        /// <param name="queueName"></param>
        /// <returns></returns>
        public bool IsSubscriptionsForQueueEmpty(string queueName) => !_subscriptionHandlers.Keys.Contains(queueName);

        /// <summary>
        /// AddSubscription
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TH"></typeparam>
        /// <param name="queueName"></param>
        public void AddSubscription<T, TH>(string queueName = null)
            where T : Event
            where TH : IEventHandler<T>
        {
            var eventName = GetEventKey<T>();

            if (!string.IsNullOrEmpty(queueName))
            {
                eventName = queueName;
            }

            DoAddSubscription(typeof(TH), eventName);

            if (!_eventTypes.Contains(typeof(T)))
            {
                _eventTypes.Add(typeof(T));
            }
        }

        /// <summary>
        /// Add to subscription
        /// </summary>
        /// <param name="handlerType"></param>
        /// <param name="eventName"></param>

        private void DoAddSubscription(Type handlerType, string eventName)
        {
            if (!HasSubscriptionsForEvent(eventName))
            {
                _subscriptionHandlers.TryAdd(eventName, new List<Subscription>());
            }

            if (_subscriptionHandlers[eventName].Any(s => s.EventHandlerType == handlerType))
            {
                throw new ArgumentException(
                     $"Event handler {handlerType.Name} already registered for '{eventName}'", nameof(handlerType));
            }

            _subscriptionHandlers[eventName].Add(Subscription.AddTyped(handlerType));
        }

        /// <summary>
        /// Remove subscription
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TH"></typeparam>
        /// <param name="queueName"></param>
        public void RemoveSubscription<T, TH>(string queueName = null)
            where TH : IEventHandler<T>
            where T : Event
        {
            Subscription eventHandlerToRemove = FindSubscriptionToRemove<T, TH>(queueName);

            string eventName = GetEventKey<T>();

            DoRemoveHandler(eventName, eventHandlerToRemove, queueName);
        }

        /// <summary>
        /// RemoveHandler
        /// </summary>
        /// <param name="eventName"></param>
        /// <param name="subsToRemove"></param>
        /// <param name="queueName"></param>
        private void DoRemoveHandler(string eventName, Subscription subsToRemove, string queueName = null)
        {
            var @event = eventName;

            if (!string.IsNullOrEmpty(queueName))
            {
                eventName = queueName;
            }

            if (subsToRemove != null)
            {
                _subscriptionHandlers[eventName].Remove(subsToRemove);

                if (!_subscriptionHandlers[eventName].Any())
                {
                    _subscriptionHandlers.TryRemove(eventName, out List<Subscription> subInfo);

                    var eventType = _eventTypes.SingleOrDefault(e => e.Name == eventName);

                    if (eventType != null)
                    {
                        _eventTypes.Remove(eventType);
                    }
                    //Raise event removed
                    RaiseOnEventRemoved(@event, queueName);
                }
            }
        }

        /// <summary>
        /// GetHandlersForEvent
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public IEnumerable<Subscription> GetHandlersForEvent<T>() where T : Event
        {
            var key = GetEventKey<T>();
            return GetHandlersForEvent(key);
        }

        /// <summary>
        /// Get Handlers For an Event
        /// </summary>
        /// <param name="eventName"></param>
        /// <returns></returns>
        public IEnumerable<Subscription> GetHandlersForEvent(string eventName) => _subscriptionHandlers[eventName];

        /// <summary>
        /// Trigger Event removed 
        /// </summary>
        /// <param name="eventName"></param>
        /// <param name="queueName"></param>
        private void RaiseOnEventRemoved(string eventName, string queueName = null)
        {
            if (string.IsNullOrEmpty(queueName))
                queueName = eventName;

            var eventMetaData = new EventMetadataEventArgs(eventName, queueName);
            var eventHandler = OnEventRemoved;
            eventHandler?.Invoke(this, eventMetaData);
        }

        /// <summary>
        /// Find Subscription To Remove
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TH"></typeparam>
        /// <param name="queueName"></param>
        /// <returns></returns>
        private Subscription FindSubscriptionToRemove<T, TH>(string queueName = null)
             where T : Event
             where TH : IEventHandler<T>
        {
            var eventName = GetEventKey<T>();

            if (!string.IsNullOrEmpty(queueName))
            {
                eventName = queueName;
            }
            return DoFindSubscriptionToRemove(eventName, typeof(TH));
        }

        /// <summary>
        /// DoFindSubscriptionToRemove
        /// </summary>
        /// <param name="eventName"></param>
        /// <param name="eventHandlerType"></param>
        /// <returns></returns>
        private Subscription DoFindSubscriptionToRemove(string eventName, Type eventHandlerType)
        {
            if (!HasSubscriptionsForEvent(eventName))
            {
                return null;
            }

            return _subscriptionHandlers[eventName].SingleOrDefault(s => s.EventHandlerType == eventHandlerType);
        }

        /// <summary>
        /// HasSubscriptionsForEvent
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public bool HasSubscriptionsForEvent<T>() where T : Event
        {
            var key = GetEventKey<T>();
            return HasSubscriptionsForEvent(key);
        }

        /// <summary>
        /// HasSubscriptionsForEvent
        /// </summary>
        /// <param name="eventName"></param>
        /// <returns></returns>
        public bool HasSubscriptionsForEvent(string eventName) => _subscriptionHandlers.ContainsKey(eventName);

        /// <summary>
        ///GetEvent ByName
        /// </summary>
        /// <param name="eventName"></param>
        /// <returns></returns>
        public Type GetEventByName(string eventName) => _eventTypes.SingleOrDefault(t => t.Name == eventName);

        /// <summary>
        ///Get EventKey
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public string GetEventKey<T>()
        {
            return typeof(T).Name;
        }
    }
}
