//*********************************************************************************************
//* File             :   MqttConnection.cs
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
using MQTTnet.Exceptions;
using Polly;
using Serilog;
using Sukanta.Resiliency;
using Sukanta.Resiliency.Abstraction;
using System;
using System.IO;
using System.Threading;

namespace Sukanta.EventBus.Mqtt
{
    /// <summary>
    /// Mqtt Connection manager
    /// </summary>
    public class MqttConnection : IMqttConnection
    {
        private readonly IResilientPolicy _resilientPolicy;
        private readonly ILogger _logger;
        private readonly int _retryCount;
        bool _disposed;
        private readonly object _lock = new object();

        /// <summary>
        /// Server IP or url
        /// </summary>
        private readonly string _mqttServerUrl;

        /// <summary>
        /// Port
        /// </summary>
        private readonly int _mqttServerPort = 1883;//default port


        /// <summary>
        /// MqttConnection
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="mqttServerUrl"></param>
        /// <param name="port"></param>
        /// <param name="resilientPolicy"></param>
        /// <param name="retryCount"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public MqttConnection(ILogger logger, string mqttServerUrl, int? port = 1883, IResilientPolicy resilientPolicy = null, int retryCount = 3)
        {
            _mqttServerUrl = mqttServerUrl ?? throw new ArgumentNullException(nameof(mqttServerUrl));
            _mqttServerPort = port.Value;

            _retryCount = retryCount;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _resilientPolicy = resilientPolicy ?? new MqttResilientPolicy(_logger, _retryCount);
        }


        /// <summary>
        /// IsConnected to RabbitMQ Broker ?
        /// </summary>
        public bool IsConnected
        {
            get
            {
                return mqttClient != null && mqttClient.IsConnected && !_disposed;
            }
        }

        /// <summary>
        /// mqtt Client
        /// </summary>
        public IMqttClient mqttClient { get; set; }

        /// <summary>
        /// mqtt Factory
        /// </summary>
        public MqttFactory mqttFactory { get; set; }

        /// <summary>
        /// Dispose connection
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;

            try
            {
                mqttClient.Dispose();
            }
            catch (IOException ex)
            {
                _logger.Error(ex, ex.Message);
            }
        }

        /// <summary>
        /// Try to connect to RabbitMQ  broker server
        /// </summary>
        /// <returns></returns>
        public bool TryConnect()
        {
            _logger.Information("Mqtt Client is trying to connect");

            //Using retry policy
            var policy = Policy.Handle<MqttCommunicationException>()
                       .Or<MqttCommunicationTimedOutException>()
                       .Or<Exception>()
                       .WaitAndRetry(_retryCount, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), (exp, time) =>
                       {
                           _logger.Warning(exp, "Mqtt client could not connect after {TimeOut}s ({ExceptionMessage})", $"{time.TotalSeconds:n1}", exp.Message);
                       }
                   );

            lock (_lock)
            {
                //connect
                policy.Execute(() =>
                  {
                      mqttFactory = new MqttFactory();

                      mqttClient = mqttFactory.CreateMqttClient();

                      var mqttClientOptions = new MqttClientOptionsBuilder()
                          .WithTcpServer(_mqttServerUrl, _mqttServerPort)
                          .Build();

                      mqttClient.ConnectAsync(mqttClientOptions, CancellationToken.None).Wait(100, CancellationToken.None);
                  });
            }

            //if connected add the handlers for connection management
            if (IsConnected)
            {
                _logger.Information("Mqtt client connected with id '{0}'", mqttClient.Options.ClientId);

                return true;
            }
            else
            {
                _logger.Fatal("Mqtt connection could not be created and opened");

                return false;
            }
        }

    }
}
