//*********************************************************************************************
//* File             :   EventHandlerCommon.cs
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
using System.Threading.Tasks;

namespace DemoEventsAndHandlers
{
    public class EventHandlerCommon : IEventHandler<Event>
    {
        public async Task Handle(Event @event)
        {
            if (@event is EventOne)
            {
                var eventData = @event as EventOne;
                Console.WriteLine($"RECEIVED EventOne : {eventData.data}");
            }
            else if (@event is EventTwo)
            {
                var eventData = @event as EventTwo;
                Console.WriteLine($"RECEIVED EventTwo  : {eventData.data}");
            }
            await Task.Delay(1);
        }

    }
}
