//*********************************************************************************************
//* File             :   ICommunicationChannel.cs
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
//* File             :   ICommunicationChannel.cs
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


namespace Sukanta.EventBus.Abstraction.Common
{
    public interface ICommunicationChannel : IChannel
    {
        /// <summary>
        /// unique Id to identify a connection to a messaging bus
        /// </summary>
        string ConnectionId { get; }

        /// <summary>
        /// Is the communication bus connected to broker ?
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Supported EventBus concrete types(RabbitMQ, SgnalR, AzureServiceBus)
        /// </summary>
        CommunicationBus CommunicationMode { get; }
    }

    /// <summary>
    /// Marker interface
    /// </summary>
    public interface IChannel
    {
    }
}

