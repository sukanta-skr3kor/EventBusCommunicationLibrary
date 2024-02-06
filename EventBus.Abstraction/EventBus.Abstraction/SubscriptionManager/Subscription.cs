//*********************************************************************************************
//* File             :   Subscription.cs
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
//* File             :   Subscription.cs
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

using System;

namespace Sukanta.EventBus.Abstraction.SubscriptionManager
{
    /// <summary>
    /// Subscription details for event
    /// </summary>
    public partial class EventBusSubscriptionManager : IEventBusSubscriptionManager
    {
        /// <summary>
        /// Subscription info
        /// </summary>
        public class Subscription
        {
            /// <summary>
            /// EventHandler Type
            /// </summary>
            public Type EventHandlerType { get; internal set; }

            /// <summary>
            /// Subscription
            /// </summary>
            /// <param name="eventHandlerType"></param>
            private Subscription(Type eventHandlerType)
            {
                EventHandlerType = eventHandlerType;
            }

            /// <summary>
            /// Add a Typed handler
            /// </summary>
            /// <param name="eventHandlerType"></param>
            /// <returns></returns>
            public static Subscription AddTyped(Type eventHandlerType)
            {
                return new Subscription(eventHandlerType);
            }
        }
    }
}