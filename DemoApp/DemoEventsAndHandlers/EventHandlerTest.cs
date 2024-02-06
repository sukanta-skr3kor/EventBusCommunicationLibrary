//*********************************************************************************************
//* File             :   EventHandlerTest.cs
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
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DemoEventsAndHandlers
{
    public class EventHandlerTest : IEventHandler<EventOne>
    {
        private readonly IEventBus _bus;
        public event EventHandler demoEventHandler;// = delegate { };

        public EventHandlerTest(IEventBus bus)
        {
            _bus = bus;
        }

        public async Task Handle(EventOne @event)
        {
            demoEventHandler?.Invoke(this, new EventData(@event));
            Volatile.Read(ref demoEventHandler).Invoke(this, new EventData(@event));
            // Console.WriteLine($"RECEIVED DATA : {@event.data}");
            await Task.Delay(1);
        }
    }

    public class EventData : EventArgs
    {
        public EventOne _errorEvent { get; set; }

        public EventData(EventOne errorEvent)
        {
            _errorEvent = errorEvent;
        }
    }
}
