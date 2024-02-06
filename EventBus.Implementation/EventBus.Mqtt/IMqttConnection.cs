//*********************************************************************************************
//* File             :   IMqttConnection.cs
//* Author           :   Rout, Sukanta 
//* Date             :   22/8/2023
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

using MQTTnet;
using MQTTnet.Client;
using System;

namespace Sukanta.EventBus.Mqtt
{
    public interface IMqttConnection : IDisposable
    {
        /// <summary>
        /// Is Connected to Mqtt Broker ?
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Mqtt Client 
        /// </summary>
        IMqttClient mqttClient { get; set; }

        /// <summary>
        /// Mqtt Factory
        /// </summary>
        MqttFactory mqttFactory { get; set; }

        /// <summary>
        /// Try connect to Mqtt Broker
        /// </summary>
        /// <returns></returns>
        bool TryConnect();

    }
}
