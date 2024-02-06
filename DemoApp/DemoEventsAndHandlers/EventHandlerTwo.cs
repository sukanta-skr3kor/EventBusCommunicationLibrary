//*********************************************************************************************
//* File             :   EventHandlerTwo.cs
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
using System.Threading.Tasks;

namespace DemoEventsAndHandlers
{
    public class EventHandlerTwo : IEventHandler<EventTwo>
    {
        private readonly IEventBus _bus;
        public EventHandlerTwo(IEventBus bus)
        {
            _bus = bus;
        }
        public async Task Handle(EventTwo @event)
        {
            Console.WriteLine($"RECEIVED DATA : {@event.data}");
            await Task.Delay(1);
        }
    }
}
