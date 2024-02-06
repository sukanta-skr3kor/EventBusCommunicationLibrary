//*********************************************************************************************
//* File             :   KafkaConnection.cs
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
using Polly;
using Serilog;
using System;
using System.Net.Sockets;

namespace Sukanta.EventBus.Kafka
{
    public class KafkaConnection : IKafkaConnection
    {
        private readonly ILogger _logger;
        private readonly ClientConfig _clientConfig;
        private readonly int _retryCount;
        bool _disposed;
        private IProducer<Null, string> _producer = null;
        private IConsumer<Null, string> _consumer = null;

        public bool IsConnected => _producer != null && _producer.Handle != null;

        /// <summary>
        ///  Kafka Connection with clientconfig
        /// </summary>
        /// <param name="clientConfig"></param>
        /// <param name="logger"></param>
        /// <param name="retryCount"></param>
        public KafkaConnection(ClientConfig clientConfig, ILogger logger, int retryCount = 5)
        {
            _logger = logger;
            _retryCount = retryCount;
            _clientConfig = clientConfig ?? new ClientConfig() { BootstrapServers = "localhost:9092", ClientId = Guid.NewGuid().ToString() };
        }

        /// <summary>
        /// Kafka Connection with server ip and port
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="serverIP"></param>
        /// <param name="port"></param>
        /// <param name="retryCount"></param>
        public KafkaConnection(ILogger logger, string serverIP = "localhost", int port = 9092, int retryCount = 5)
        {
            _logger = logger;
            _retryCount = retryCount;
            _clientConfig = new ClientConfig() { BootstrapServers = $"{serverIP}{":"}{port}", ClientId = Guid.NewGuid().ToString() };
        }

        /// <summary>
        ///  Producer for the message topic
        /// </summary>
        /// <param name="topicName"></param>
        /// <returns></returns>
        public IProducer<Null, string> GetProducer(string topicName)
        {
            try
            {
                if (_producer == null || _producer.Handle == null)
                {
                    //Using retry policy
                    var policy = Policy.Handle<SocketException>()
                     .Or<KafkaException>()
                     .WaitAndRetry(_retryCount, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), (ex, time) =>
                     {
                         _logger.Warning(ex, "Could not create kafka producer after {Timeout}s ({ExceptionMessage})", $"{time.TotalSeconds:n1}", ex.Message);
                     });

                    policy.Execute(() =>
                    {
                        _producer = new ProducerBuilder<Null, string>(_clientConfig).Build();
                    });
                }
            }
            catch
            {
                throw;
            }

            return _producer;
        }


        /// <summary>
        ///  Consumer for the message
        /// </summary>
        /// <param name="topicName"></param>
        /// <returns></returns>
        public IConsumer<Null, string> GetConsumer(string topicName)
        {
            try
            {
                var consumerConfig = new ConsumerConfig(_clientConfig);

                consumerConfig.GroupId = $"{topicName}{"_"}{Guid.NewGuid()}";
                consumerConfig.AutoOffsetReset = AutoOffsetReset.Earliest;

                //Using retry policy
                var policy = Policy.Handle<SocketException>()
                .Or<KafkaException>()
                .WaitAndRetry(_retryCount, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), (ex, time) =>
                {
                    _logger.Warning(ex, "Could not create kafka consumer after {Timeout}s ({ExceptionMessage})", $"{time.TotalSeconds:n1}", ex.Message);
                });

                policy.Execute(() =>
                {
                    _consumer = new ConsumerBuilder<Null, string>(consumerConfig).Build();
                });
            }
            catch
            {
                throw;
            }

            return _consumer;
        }

        /// <summary>
        /// Dispose producer and consumer channels
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                if (_producer != null)
                {
                    _producer.Flush(TimeSpan.FromSeconds(1));
                    _producer?.Dispose();
                }

                if (_consumer != null)
                {
                    _consumer.Unsubscribe();
                    _consumer.Close();
                    _consumer?.Dispose();
                }
            }
            catch (KafkaException exp)
            {
                _logger.Error(exp, exp.Message);
            }
        }

    }
}
