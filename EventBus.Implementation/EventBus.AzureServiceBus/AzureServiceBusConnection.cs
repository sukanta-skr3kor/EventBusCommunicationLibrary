//*********************************************************************************************
//* File             :   AzureServiceBusConnection.cs
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

using Microsoft.Azure.ServiceBus;
using Serilog;
using System;

namespace Sukanta.EventBus.AzureServiceBus
{
    /// <summary>
    ///AzureServiceBusConnection
    /// </summary>
    public class AzureServiceBusConnection : IAzureServiceBusConnection
    {
        private readonly ILogger _logger;
        private readonly ServiceBusConnectionStringBuilder _serviceBusConnectionStringBuilder;
        private ITopicClient _azureServiceBusTopicClient;
        private IQueueClient _azureServiceBusQueueClient;
        bool _disposed;

        /// <summary>
        /// Azure ServiceBus ConnectionString
        /// </summary>
        public ServiceBusConnectionStringBuilder AzureServiceBusConnectionString => _serviceBusConnectionStringBuilder;

        /// <summary>
        /// AzureServiceBusConnection
        /// </summary>
        /// <param name="serviceBusConnectionStringBuilder"></param>
        /// <param name="logger"></param>
        public AzureServiceBusConnection(ServiceBusConnectionStringBuilder serviceBusConnectionStringBuilder, ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _serviceBusConnectionStringBuilder = serviceBusConnectionStringBuilder ??
               throw new ArgumentNullException(nameof(serviceBusConnectionStringBuilder));

            _azureServiceBusTopicClient = new TopicClient(_serviceBusConnectionStringBuilder, RetryPolicy.Default);
        }

        /// <summary>
        /// CreateTopicClient
        /// </summary>
        /// <returns></returns>
        public ITopicClient CreateTopicClient()
        {
            if (_azureServiceBusTopicClient.IsClosedOrClosing)
            {
                _azureServiceBusTopicClient = new TopicClient(_serviceBusConnectionStringBuilder, RetryPolicy.Default);
            }

            return _azureServiceBusTopicClient;
        }

        /// <summary>
        /// CreateQueueClient
        /// </summary>
        /// <returns></returns>
        public IQueueClient CreateQueueClient(string queueName)
        {
            if (_azureServiceBusQueueClient.IsClosedOrClosing)
            {
                _azureServiceBusQueueClient = new QueueClient(_serviceBusConnectionStringBuilder.GetEntityConnectionString(), queueName, ReceiveMode.PeekLock
                    , RetryPolicy.Default);
            }
            return _azureServiceBusQueueClient;
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
