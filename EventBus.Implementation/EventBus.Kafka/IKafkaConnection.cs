//*********************************************************************************************
//* File             :   IKafkaConnection.cs
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

using Confluent.Kafka;
using System;

namespace Sukanta.EventBus.Kafka
{
    public interface IKafkaConnection : IDisposable
    {
        /// <summary>
        /// Is Connected to Kafka server?
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Create a producer to produce messages for a topic
        /// </summary>
        /// <param name="topicName"></param>
        /// <returns></returns>
        IProducer<Null, string> GetProducer(string topicName);

        /// <summary>
        /// Create a consumer to consume messages from a topic
        /// </summary>
        /// <returns></returns>
        IConsumer<Null, string> GetConsumer(string topicName);
    }
}
