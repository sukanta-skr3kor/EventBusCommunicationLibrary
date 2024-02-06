//*********************************************************************************************
//* File             :   Event.cs
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
//* File             :   Event.cs
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

using Newtonsoft.Json;
using System;

namespace Sukanta.EventBus.Abstraction.Events
{
    /// <summary>
    /// Event base class
    /// </summary>
    public class Event
    {
        /// <summary>
        /// Event/message Id
        /// </summary>
        [JsonProperty]
        public string Id { get; protected set; }

        /// <summary>
        /// Event/message Name
        /// </summary>
        [JsonProperty]
        public string Name { get; protected set; }

        /// <summary>
        /// Event/message creation time in utc
        /// </summary>
        [JsonProperty]
        public DateTime Timestamp { get; protected set; }

        [JsonConstructor]
        protected Event()
        {
            Timestamp = DateTime.UtcNow;
            Id = Guid.NewGuid().ToString();
            Name = this.GetType().Name;
        }
    }
}




