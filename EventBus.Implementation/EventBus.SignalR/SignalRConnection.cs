//*********************************************************************************************
//* File             :   SignalRConnection.cs
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

using Microsoft.AspNet.SignalR.Client;
using Polly;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;

namespace Sukanta.EventBus.SignalR
{
    /// <summary>
    ///SignalR Connection Manager
    /// </summary>
    public class SignalRConnection : ISignalRConnection
    {
        private readonly string _signalRServer;
        private HubConnection _connection { get; set; }
        private readonly int _retryCount;
        private readonly List<string> _hubNames;
        private readonly object _lock = new object();
        private readonly ILogger _logger;
        private bool _disposed;

        /// <summary>
        /// Dictionaryto hold hubproxy
        /// </summary>
        public ConcurrentDictionary<string, IHubProxy> _hubProxyDetails { get; set; }

        /// <summary>
        /// SignalR ConnectionId
        /// </summary>
        public string ConnectionId { get; set; }

        /// <summary>
        /// IsConnected to SignalR Server
        /// </summary>
        public bool IsConnected
        {
            get
            {
                return _connection != null && _connection.State == ConnectionState.Connected;
            }
        }

        /// <summary>
        /// SignalRCommunication
        /// </summary>
        /// <param name="signalRServer"></param>
        /// <param name="hubNames"></param>
        /// <param name="retryCount"></param>
        public SignalRConnection(string signalRServer, List<string> hubNames, ILogger logger, int retryCount = 5)
        {
            _signalRServer = signalRServer ?? throw new ArgumentNullException(nameof(signalRServer));
            _hubNames = hubNames;
            _retryCount = retryCount;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _hubProxyDetails = new ConcurrentDictionary<string, IHubProxy>();
        }

        /// <summary>
        /// dispose connection
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
                _logger.Error(ex.ToString());
            }
        }

        /// <summary>
        /// Connect to SignalR Server
        /// </summary>
        /// <returns></returns>
        public bool TryConnect()
        {
            try
            {
                lock (_lock)
                {
                    var policy = Policy.Handle<SocketException>()
                        .WaitAndRetry(_retryCount, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), (ex, time) =>
                        {
                            _logger.Warning(ex, "SignalR Client could not connect after {TimeOut}s ({ExceptionMessage})", $"{time.TotalSeconds:n1}", ex.Message);
                        }
                    );

                    policy.Execute(() =>
                    {
                        _connection = new HubConnection(_signalRServer);
                        CreateHubProxy();
                        _connection.Start().Wait();
                        _connection.DeadlockErrorTimeout = new TimeSpan(0, 5, 0);
                        ConnectionId = _connection.ConnectionId;
                    });

                    if (IsConnected)
                    {
                        _connection.Closed += ConnectionClosed;
                        _connection.Reconnecting += ConnectionReconnecting;
                        _connection.Error += ConnectionError;

                        _logger.Information("SignalR Client acquired a persistent connection to '{HostName}' and is subscribed to failure events", _connection?.Url);

                        return true;
                    }
                    else
                    {
                        _logger.Fatal("SignalR connections could not be created and opened");

                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex.Message);
                throw;
            }
        }

        /// <summary>
        /// CreateHubProxy
        /// </summary>
        public void CreateHubProxy()
        {
            foreach (var hub in _hubNames)
            {
                _hubProxyDetails.TryAdd($"{hub}Hub", _connection.CreateHubProxy($"{hub}Hub"));
                _logger.Information($"SignalR Hub {hub} created");
            }
        }

        /// <summary>
        /// Connection Error
        /// </summary>
        /// <param name="obj"></param>
        private void ConnectionError(Exception obj)
        {
            if (_disposed) return;

            _logger.Warning($"SignalR connection error occured. Trying to reconnect...{obj}");

            TryConnect();
        }

        /// <summary>
        /// Connection Reconnecting
        /// </summary>
        private void ConnectionReconnecting()
        {
            if (_disposed) return;

            _logger.Warning("SignalR connection is reconnecting. Trying to reconnect...");
        }

        /// <summary>
        /// Connection Closed
        /// </summary>
        private void ConnectionClosed()
        {
            if (_disposed) return;

            _logger.Warning("SignalR connection is closed. Trying to reconnect...");

            TryConnect();
        }
    }
}
