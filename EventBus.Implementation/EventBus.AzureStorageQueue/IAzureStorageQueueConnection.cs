//*********************************************************************************************
//* File             :   IAzureStorageQueueConnection.cs
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

namespace Sukanta.EventBus.AzureStorageQueue
{
    using Microsoft.Azure.Storage;
    using Microsoft.Azure.Storage.Queue;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// APIs for AzureServiceBus Connection
    /// </summary>
    public interface IAzureStorageQueueConnection : IDisposable
    {
        /// <summary>
        /// CloudStorage Queues
        /// </summary>
        IEnumerable<CloudQueue> AvailableQueues { get; }

        /// <summary>
        /// CloudStorageAccount
        /// </summary>
        CloudStorageAccount cloudStorageAccount { get; set; }

        /// <summary>
        /// Create CloudQueue
        /// </summary>
        /// <param name="queueName"></param>
        /// <returns></returns>
        CloudQueue GetQueue(string queueName);

        /// <summary>
        /// Create CloudQueueClient
        /// </summary>
        /// <returns></returns>
        CloudQueueClient CreateQueueClient();
    }
}