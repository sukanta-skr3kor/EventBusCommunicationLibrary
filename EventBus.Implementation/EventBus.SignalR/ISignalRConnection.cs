//*********************************************************************************************
//* File             :   ISignalRConnection.cs
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
using System;
using System.Collections.Concurrent;

namespace Sukanta.EventBus.SignalR
{
    /// <summary>
    /// APIs for SignalR Connection
    /// </summary>
    public interface ISignalRConnection : IDisposable
    {
        /// <summary>
        /// SignalR connectionId
        /// </summary>
        string ConnectionId { get; set; }

        /// <summary>
        /// Is Connected to signalr server ?
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Try connect to SignalR server
        /// </summary>
        /// <returns></returns>
        bool TryConnect();

        /// <summary>
        /// Create Hub proxy
        /// </summary>
        void CreateHubProxy();

        /// <summary>
        /// Hbproxy details information
        /// </summary>
        ConcurrentDictionary<string, IHubProxy> _hubProxyDetails { get; set; }
    }
}
