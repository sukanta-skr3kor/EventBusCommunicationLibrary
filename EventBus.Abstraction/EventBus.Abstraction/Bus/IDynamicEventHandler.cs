//*********************************************************************************************
//* File             :   IDynamicEventHandler.cs
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
//* File             :   IDynamicEventHandler.cs
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

using System.Threading.Tasks;

namespace Sukanta.EventBus.Abstraction.Bus
{
    /// <summary>
    /// Dynamic Event Handler
    /// </summary>
    public interface IDynamicEventHandler
    {
        /// <summary>
        /// Handle a dynamic event
        /// </summary>
        /// <param name="eventData"></param>
        /// <returns></returns>
        Task Handle(dynamic eventData);
    }
}
