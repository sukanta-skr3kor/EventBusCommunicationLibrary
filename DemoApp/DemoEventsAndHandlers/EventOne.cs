//*********************************************************************************************
//* File             :   EventOne.cs
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
using Newtonsoft.Json;

namespace DemoEventsAndHandlers
{
    public class EventOne : Event
    {
        [JsonProperty]
        public string data { get; set; }
    }
}
