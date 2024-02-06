//*********************************************************************************************
//* File             :   IRabbitMQConnection.cs
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

using RabbitMQ.Client;
using System;

namespace Sukanta.EventBus.RabbitMQ
{
    public interface IRabbitMQConnection : IDisposable
    {
        /// <summary>
        /// Event to notify when there is server disconnect to the clients
        /// </summary>
        event EventHandler ServerDisConnect;

        /// <summary>
        /// Is Connected to RabbitMQ Broker ?
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Try connect to RabbitMQ Broker
        /// </summary>
        /// <returns></returns>
        bool TryConnect();

        /// <summary>
        /// Create Model
        /// </summary>
        /// <returns></returns>
        IModel CreateModel();
    }
}
