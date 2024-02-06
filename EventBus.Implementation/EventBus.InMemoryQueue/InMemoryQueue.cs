//*********************************************************************************************
//* File             :   InMemoryQueue.cs
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
using System.Collections.Concurrent;

namespace Sukanta.EventBus.InMemoryQueue
{
    public class InMemoryQueue<T> : BlockingCollection<T>, IInMemoryQueue where T : Event
    {
        public string Name { get; set; }
        public int QueueSize { get; set; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        /// <param name="queueSize"></param>
        public InMemoryQueue(string name, int queueSize = 100000)
        {
            Name = name;
            QueueSize = queueSize;
        }
    }
}
