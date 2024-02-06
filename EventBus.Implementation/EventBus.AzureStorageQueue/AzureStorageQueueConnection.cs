//*********************************************************************************************
//* File             :   AzureStorageQueueConnection.cs
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

using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Auth;
using Microsoft.Azure.Storage.Queue;
using Serilog;
using System;
using System.Collections.Generic;

namespace Sukanta.EventBus.AzureStorageQueue
{
    /// <summary>
    ///Azure StorageQueue Connection
    /// </summary>
    public class AzureStorageQueueConnection : IAzureStorageQueueConnection
    {
        private readonly ILogger _logger;
        private readonly string _storageConnectionString;
        private CloudQueueClient _cloudStorageQueueClient = null;
        private bool _disposed;

        /// <summary>
        ///cloudStorageAccount
        /// </summary>
        public CloudStorageAccount cloudStorageAccount { get; set; }

        /// <summary>
        /// Storage credentials
        /// </summary>
        public StorageCredentials _credentials { get; set; }

        /// <summary>
        /// Base storage uri
        /// </summary>
        public Uri _baseUri { get; set; }

        /// <summary>
        /// Avilable queue list in the storage account
        /// </summary>
        public IEnumerable<CloudQueue> AvailableQueues => cloudStorageAccount?.CreateCloudQueueClient()?.ListQueues();

        /// <summary>
        /// Azure StorageQueue Connection with connectionstring
        /// </summary>
        /// <param name="storageConnectionString"></param>
        /// <param name="logger"></param>
        public AzureStorageQueueConnection(string storageConnectionString, ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _storageConnectionString = storageConnectionString ?? throw new ArgumentNullException(nameof(storageConnectionString));

            CreateCloudStorageAccount(storageConnectionString);
        }

        /// <summary>
        /// Azure storage queue with Azure AD auth
        /// </summary>
        /// <param name="baseUri"></param>
        /// <param name="credentials"></param>
        /// <param name="logger"></param>
        public AzureStorageQueueConnection(Uri baseUri, StorageCredentials credentials, ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
            _baseUri = baseUri ?? throw new ArgumentNullException(nameof(baseUri));
        }

        /// <summary>
        /// Create StorageAccount from connectionstring
        /// </summary>
        /// <param name="storageConnectionString"></param>
        private void CreateCloudStorageAccount(string storageConnectionString)
        {
            CloudStorageAccount cloudStorageAcc;

            if (!CloudStorageAccount.TryParse(storageConnectionString, out cloudStorageAcc))
            {
                _logger.Fatal("FATAL ERROR , couldn't able to create CloudStorageAccount, check connectionstring");
            }
            else
            {
                cloudStorageAccount = cloudStorageAcc;

                _logger.Information("Connected to storage account");
            }
        }

        /// <summary>
        /// Get Cloud QueueClient
        /// </summary>
        /// <returns></returns>
        public CloudQueueClient CreateQueueClient()
        {
            if (cloudStorageAccount == null)//Create queue client from storagecredentials provided
            {
                if (_credentials != null && _baseUri != null && _cloudStorageQueueClient == null)
                {
                    // Create the queue client.
                    _cloudStorageQueueClient = new CloudQueueClient(_baseUri, _credentials);
                }
            }
            else
            {
                // Create the queue client from cloudstorageaccount from connectionstring provided
                if (_cloudStorageQueueClient == null)
                {
                    _cloudStorageQueueClient = cloudStorageAccount?.CreateCloudQueueClient();
                }
            }

            return _cloudStorageQueueClient;
        }


        /// <summary>
        ///  Get Cloud Queue
        /// </summary>
        /// <param name="queueName"></param>
        /// <returns></returns>
        public CloudQueue GetQueue(string queueName)
        {
            if (CreateQueueClient() != null)
            {
                // Retrieve a reference to a queue.
                return _cloudStorageQueueClient.GetQueueReference(queueName);
            }
            return null;
        }

        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;
        }
    }
}
