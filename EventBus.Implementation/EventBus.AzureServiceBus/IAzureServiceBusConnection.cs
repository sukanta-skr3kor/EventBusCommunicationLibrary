//*********************************************************************************************
//* File             :   IAzureServiceBusConnection.cs
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

namespace Sukanta.EventBus.AzureServiceBus
{
    using Microsoft.Azure.ServiceBus;
    using System;

    /// <summary>
    /// APIs for AzureServiceBus Connection
    /// </summary>
    public interface IAzureServiceBusConnection : IDisposable
    {
        /// <summary>
        ///AzureServiceBus ConnectionString
        /// </summary>
        ServiceBusConnectionStringBuilder AzureServiceBusConnectionString { get; }

        /// <summary>
        /// Topic Client
        /// </summary>
        /// <returns></returns>
        ITopicClient CreateTopicClient();

        /// <summary>
        /// Queue Client
        /// </summary>
        /// <param name="queueName"></param>
        /// <returns></returns>
        IQueueClient CreateQueueClient(string queueName);
    }
}