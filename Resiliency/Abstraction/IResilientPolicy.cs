//*********************************************************************************************
//* File             :   IResilientPolicy.cs
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
using Polly;
using Polly.Retry;
using Polly.Wrap;
using Serilog;

namespace Sukanta.Resiliency.Abstraction
{
    /// <summary>
    /// Supported policies
    /// </summary>
    public interface IResilientPolicy : IPolicyConfiguration
    {
        /// <summary>
        /// Wait for the mentioned timepeiod and retry again based on the retry count provided
        /// </summary>
        Policy WaitAndRetryPolicy { get; set; }

        /// <summary>
        ///Async Wait for the mentioned timepeiod and retry again based on the retry count provided
        /// </summary>
        AsyncPolicy WaitAndRetryPolicyAsync { get; set; }

        /// <summary>
        /// Timeout after the mentioned time period if no response received by the server
        /// </summary>
        Policy TimeoutPolicy { get; set; }

        /// <summary>
        /// Async Timeout after the mentioned time period if no response received by the server
        /// </summary>
        AsyncPolicy TimeoutPolicyAsync { get; set; }

        /// <summary>
        /// Circuit breaker policy, breaks for th erequred amount of time (BreakDuration) and restarts the service again
        /// </summary>
        Policy CircuitBreakerPolicy { get; set; }

        /// <summary>
        /// Async Circuit breaker policy, breaks for th erequred amount of time (BreakDuration) and restarts the service again
        /// </summary>
        AsyncPolicy CircuitBreakerPolicyAsync { get; set; }

        /// <summary>
        /// Retry policy, executes for the number of times(retry count) provided if no response received from server
        /// </summary>
        RetryPolicy RetryPolicy { get; set; }

        /// <summary>
        ///Async Retry policy, executes for the number of times(retry count) provided if no response received from server
        /// </summary>
        AsyncRetryPolicy RetryPolicyAsync { get; set; }

        /// <summary>
        /// It's a common Wrap policy of Retry, TimeOut, WaitAndRetry and CircuitBreaker policy
        /// </summary>
        PolicyWrap CommonResilienceWrapPolicy { get; set; }

        /// <summary>
        /// It's a common Wrap policy of Retry, TimeOut, WaitAndRetry and CircuitBreaker policy
        /// </summary>
        AsyncPolicyWrap CommonResilienceWrapPolicyAsync { get; set; }

        /// <summary>
        /// Logger instance to log 
        /// </summary>
        ILogger Logger { get; set; }
    }

    /// <summary>
    /// PolicyConfiguration
    /// </summary>
    public interface IPolicyConfiguration : ICircuitBreakerPolicyConfig
    {
        /// <summary>
        /// Timeout value for a Policy
        /// </summary>
        int TimeoutValue { get; set; }
    }

    /// <summary>
    ///CircuitBreaker Policy Config
    ///Retry count
    ///Break Duration
    /// </summary>
    public interface ICircuitBreakerPolicyConfig : IRetryPolicyConfig
    {
        /// <summary>
        /// Break duration time for the CircuitBreaker policy
        /// </summary>
        int BreakDuration { get; set; }
    }

    /// <summary>
    /// Retry count
    /// </summary>
    public interface IRetryPolicyConfig : IPolicyConfig
    {
        /// <summary>
        /// Retry count for the Policy
        /// </summary>
        int RetryCount { get; set; }
    }

    /// <summary>
    /// Base PolicyConfig
    /// </summary>
    public interface IPolicyConfig
    {
        /// <summary>
        /// Policy name 
        /// </summary>
        string PolicyName { get; set; }
    }

}
