//*********************************************************************************************
//* File             :   IInMemoryQueue.cs
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

namespace Sukanta.EventBus.InMemoryQueue
{
    public interface IInMemoryQueue
    {
        string Name { get; set; }
        int QueueSize { get; set; }
    }
}
