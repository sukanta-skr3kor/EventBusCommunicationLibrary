//*********************************************************************************************
//* File             :   IEventBusSubscriptionManager.cs
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
//* File             :   IEventBusSubscriptionManager.cs
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
using System.Collections.Generic;
using static Sukanta.EventBus.Abstraction.SubscriptionManager.EventBusSubscriptionManager;

namespace Sukanta.EventBus.Abstraction.SubscriptionManager
{
    /// <summary>
    /// Subscription manager APIs for event bus, to register and deregister event handlers
    /// </summary>
    public interface IEventBusSubscriptionManager
    {
        bool IsSubscriptionsEmpty { get; }

        bool IsSubscriptionsForQueueEmpty(string queueName);

        void ClearSubscriptions();

        Type GetEventByName(string eventName);

        event EventHandler<EventMetadataEventArgs> OnEventRemoved;

        void AddSubscription<T, TH>(string queueName = null) where T : Event where TH : IEventHandler<T>;

        void RemoveSubscription<T, TH>(string queueName = null) where TH : IEventHandler<T> where T : Event;

        bool HasSubscriptionsForEvent<T>() where T : Event;

        bool HasSubscriptionsForEvent(string eventName);

        IEnumerable<Subscription> GetHandlersForEvent<T>() where T : Event;

        IEnumerable<Subscription> GetHandlersForEvent(string eventName);

        string GetEventKey<T>();
    }

    /// <summary>
    /// EventMetadataArgs
    /// </summary>
    public class EventMetadataEventArgs : EventArgs
    {
        public string EventName { get; set; }
        public string QueueName { get; set; }

        public EventMetadataEventArgs(string eventName, string queueName)
        {
            EventName = eventName;
            QueueName = queueName;
        }
    }

}