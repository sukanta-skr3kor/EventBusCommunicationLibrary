//*********************************************************************************************
//* File             :   IEventHandler.cs
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
//* File             :   IEventHandler.cs
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

using Sukanta.EventBus.Abstraction.Events;
using System.Threading.Tasks;

namespace Sukanta.EventBus.Abstraction.Bus
{
    /// <summary>
    /// Event Handler
    /// </summary>
    /// <typeparam name="TEvent"></typeparam>
    public interface IEventHandler<in TEvent> : IEventHandler
       where TEvent : Event
    {
        /// <summary>
        /// Handle an event
        /// </summary>
        /// <param name="event"></param>
        /// <returns></returns>
        Task Handle(TEvent @event);
    }

    /// <summary>
    /// Marker interface
    /// </summary>
    public interface IEventHandler
    {
    }
}
