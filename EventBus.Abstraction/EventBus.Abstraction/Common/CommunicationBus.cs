//*********************************************************************************************
//* File             :   CommunicationBus.cs
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
//* File             :   CommunicationBus.cs
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
    /// <summary>
    /// Supported EventBus (Messaging channels)
    /// </summary>
    public enum CommunicationBus
    {
        RabbitMQ,
        SignalR,
        AzureServiceBus,
        AzureStorageQueue,
        Kafka,
        Mqtt,
        Redis,
        InMemory
    }
}
