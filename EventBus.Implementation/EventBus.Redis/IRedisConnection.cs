//*********************************************************************************************
//* File             :   IRedisConnection.cs
//* Author           :   Rout, Sukanta 
//* Date             :   31/8/2023
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

using StackExchange.Redis;
using System;

namespace Sukanta.EventBus.Redis
{
    public interface IRedisConnection : IDisposable
    {
        /// <summary>
        /// Is Connected to Redis ?
        /// </summary>
        bool IsConnected();

        /// <summary>
        /// Try Connect
        /// </summary>
        /// <returns></returns>
        bool TryConnect();

        /// <summary>
        /// Get the connecition for Redis
        /// </summary>
        /// <returns></returns>
        IConnectionMultiplexer GetConnection();

    }
}
