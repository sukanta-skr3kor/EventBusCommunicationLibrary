//*********************************************************************************************
//* File             :   IEventBus.cs
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
//* File             :   IEventBus.cs
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

using Sukanta.EventBus.Abstraction.Common;
using Sukanta.EventBus.Abstraction.Events;

namespace Sukanta.EventBus.Abstraction.Bus
{
    /// <summary>
    /// Event bus APIs
    /// </summary>
    public interface IEventBus : ICommunicationChannel
    {
        /// <summary>
        /// Publish an event to the eventbus
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="event"></param>
        void Publish<T>(T @event) where T : Event;

        /// <summary>
        /// Publish an event to the eventbus to a specific queue
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="event"></param>
        /// <param name="queueName"></param>
        void Publish<T>(T @event, string queueName) where T : Event;

        /// <summary>
        /// Subscribe to the event handler
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TH"></typeparam>
        void Subscribe<T, TH>() where T : Event where TH : IEventHandler<T>;

        /// <summary>
        /// Subscribe to the event handler from a specific Queue
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TH"></typeparam>
        /// <param name="queueName"></param>
        void Subscribe<T, TH>(string queueName) where T : Event where TH : IEventHandler<T>;

        /// <summary>
        /// Unsubscribe from the event handler
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TH"></typeparam>
        /// <param name="queueName"></param>
        void Unsubscribe<T, TH>(string queueName = null) where T : Event where TH : IEventHandler<T>;
    }
}