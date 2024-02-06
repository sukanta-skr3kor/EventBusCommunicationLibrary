//*********************************************************************************************
//* File             :   RabbitMQConnection.cs
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

using Sukanta.Resiliency;
using Sukanta.Resiliency.Abstraction;
using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using Serilog;
using System;
using System.IO;
using System.Net.Sockets;

namespace Sukanta.EventBus.RabbitMQ
{
    /// <summary>
    /// RabbitMQ Connection manager
    /// </summary>
    public class RabbitMQConnection : IRabbitMQConnection
    {
        private readonly IResilientPolicy _resilientPolicy;
        private readonly IConnectionFactory _connectionFactory;
        private readonly ILogger _logger;
        private readonly int _retryCount;
        IConnection _connection;
        bool _disposed;
        private readonly object _lock = new object();


        private event EventHandler _ServerDisConnect;

        /// <summary>
        ///Raise server disconnect event
        /// </summary>
        public event EventHandler ServerDisConnect
        {
            add
            {
                _ServerDisConnect += new EventHandler(value);
            }

            remove
            {
                _ServerDisConnect -= new EventHandler(value);
            }
        }


        /// <summary>
        /// RabbitMQ Connection
        /// </summary>
        /// <param name="connectionFactory"></param>
        /// <param name="resilientPolicy"></param>
        /// <param name="logger"></param>
        /// <param name="retryCount"></param>
        public RabbitMQConnection(IConnectionFactory connectionFactory, ILogger logger, IResilientPolicy resilientPolicy = null, int retryCount = 5)
        {
            _retryCount = retryCount;
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _resilientPolicy = resilientPolicy ?? new RabbitMQResilientPolicy(_logger, _retryCount);
        }

        /// <summary>
        /// RabbitMQ Connection
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="hostName"></param>
        /// <param name="userName"></param>
        /// <param name="password"></param>
        /// <param name="retryCount"></param>
        public RabbitMQConnection(ILogger logger, string hostName = "localhost", string userName = "guest", string password = "guest", IResilientPolicy resilientPolicy = null, int retryCount = 5)
        {
            _connectionFactory = new ConnectionFactory()
            {
                HostName = hostName,
                UserName = userName,
                Password = password,
                DispatchConsumersAsync = true
            };
            _retryCount = retryCount;

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _resilientPolicy = resilientPolicy ?? new RabbitMQResilientPolicy(_logger, _retryCount);
        }

        /// <summary>
        /// IsConnected to RabbitMQ Broker ?
        /// </summary>
        public bool IsConnected
        {
            get
            {
                return _connection != null && _connection.IsOpen && !_disposed;
            }
        }

        /// <summary>
        /// Create Model
        /// </summary>
        /// <returns></returns>
        public IModel CreateModel()
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("No RabbitMQ connection is available.");
            }
            return _connection.CreateModel();
        }

        /// <summary>
        /// Dispose connection
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;

            try
            {
                _connection.Dispose();
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
            _logger.Information("RabbitMQ Client is trying to connect");

            lock (_lock)
            {
                //Using retry policy
                var policy = Policy.Handle<SocketException>()
                           .Or<BrokerUnreachableException>()
                           .WaitAndRetry(_retryCount, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), (exp, time) =>
                           {
                               _logger.Warning(exp, "RabbitMQ client could not connect after {TimeOut}s ({ExceptionMessage})", $"{time.TotalSeconds:n1}", exp.Message);
                           }
                       );

                //connect
                policy.Execute(() =>
                  {
                      _connection = _connectionFactory
                            .CreateConnection();
                  });

                //if connected add the handlers for connection management
                if (IsConnected)
                {
                    _connection.ConnectionShutdown += OnConnectionShutdown;
                    _connection.CallbackException += OnCallbackException;
                    _connection.ConnectionBlocked += OnConnectionBlocked;

                    _logger.Information("RabbitMQ client connected to '{HostName}'", _connection.Endpoint.HostName);

                    return true;
                }
                else
                {
                    _logger.Fatal("RabbitMQ connection could not be created and opened");

                    return false;
                }
            }
        }

        /// <summary>
        /// OnConnection Blocked
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnConnectionBlocked(object sender, ConnectionBlockedEventArgs e)
        {
            if (_disposed)
                return;

            _logger.Warning("RabbitMQ connection is shuting down(Blocked). Trying to reconnect...");

            //raise server disconnect event
            _ServerDisConnect?.Invoke(sender, e);

            TryConnect();
        }

        /// <summary>
        /// OnCallback Exception
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void OnCallbackException(object sender, CallbackExceptionEventArgs e)
        {
            if (_disposed)
                return;

            _logger.Warning("RabbitMQ connection thrown an exception. Trying to reconnect...");

            //raise server disconnect event
            _ServerDisConnect?.Invoke(sender, e);

            TryConnect();
        }

        /// <summary>
        /// OnConnection Shutdown
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="reason"></param>
        void OnConnectionShutdown(object sender, ShutdownEventArgs reason)
        {
            if (_disposed)
                return;

            _logger.Warning("RabbitMQ connection is on shutdown. Trying to reconnect...");

            //raise server disconnect event
            _ServerDisConnect?.Invoke(sender, reason);

            TryConnect();
        }
    }
}
