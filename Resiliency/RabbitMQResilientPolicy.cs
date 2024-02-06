//*********************************************************************************************
//* File             :   RabbitMQResilientPolicy.cs
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

using Sukanta.Resiliency.Abstraction;
using Polly;
using Polly.Retry;
using Polly.Timeout;
using Polly.Wrap;
using RabbitMQ.Client.Exceptions;
using Serilog;
using System;
using System.Net.Sockets;

namespace Sukanta.Resiliency
{
    /// <summary>
    /// RabbotMQ resiliency policy
    /// </summary>
    public class RabbitMQResilientPolicy : ResilientPolicy
    {
        //Policy data
        public override string PolicyName { get; set; } = "RabbitMQPolicy";
        public override int RetryCount { get; set; }
        public override int TimeoutValue { get; set; }
        public override int BreakDuration { get; set; }
        public override ILogger Logger { get; set; }

        //Non Async policy
        public override Policy WaitAndRetryPolicy { get; set; }
        public override RetryPolicy RetryPolicy { get; set; }
        public override Policy TimeoutPolicy { get; set; }
        public override Policy CircuitBreakerPolicy { get; set; }
        public override PolicyWrap CommonResilienceWrapPolicy { get; set; }

        //Async policy
        public override AsyncPolicy WaitAndRetryPolicyAsync { get; set; }
        public override AsyncRetryPolicy RetryPolicyAsync { get; set; }
        public override AsyncPolicy TimeoutPolicyAsync { get; set; }
        public override AsyncPolicy CircuitBreakerPolicyAsync { get; set; }
        public override AsyncPolicyWrap CommonResilienceWrapPolicyAsync { get; set; }


        /// <summary>
        ///RabbitMQResilientPolicy
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="breakDuration"></param>
        /// <param name="retryCount"></param>
        /// <param name="timeOut"></param>
        public RabbitMQResilientPolicy(ILogger logger, int breakDuration = 1, int retryCount = 3, int timeOut = 5)
        {
            TimeoutValue = timeOut;
            RetryCount = retryCount;
            BreakDuration = breakDuration;
            Logger = logger;

            TimeoutPolicy = Policy.Timeout(timeOut, TimeoutStrategy.Optimistic);
            TimeoutPolicyAsync = Policy.TimeoutAsync(timeOut, TimeoutStrategy.Optimistic);

            RetryPolicy = Policy.Handle<SocketException>().Or<BrokerUnreachableException>().Retry(retryCount);
            RetryPolicyAsync = Policy.Handle<SocketException>().Or<BrokerUnreachableException>().RetryAsync(retryCount);


            WaitAndRetryPolicy = Policy.Handle<SocketException>().Or<BrokerUnreachableException>()
                .WaitAndRetry(retryCount, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), (ex, time) =>
                {
                    Logger.Error(ex, "RabbitMQ client could not connect after {TimeOut}s ({ExceptionMessage})", $"{time.TotalSeconds:n1}", ex.Message);
                });
            WaitAndRetryPolicyAsync = Policy.Handle<SocketException>().Or<BrokerUnreachableException>()
               .WaitAndRetryAsync(retryCount, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), (ex, time) =>
               {
                   Logger.Error(ex, "RabbitMQ client could not connect after {TimeOut}s ({ExceptionMessage})", $"{time.TotalSeconds:n1}", ex.Message);
               });


            CircuitBreakerPolicy = Policy.Handle<SocketException>().Or<BrokerUnreachableException>()
                                  .CircuitBreaker(retryCount, TimeSpan.FromMilliseconds(BreakDuration));
            CircuitBreakerPolicyAsync = Policy.Handle<SocketException>().Or<BrokerUnreachableException>()
                                 .CircuitBreakerAsync(retryCount, TimeSpan.FromMilliseconds(BreakDuration));


            CommonResilienceWrapPolicy = Policy.Wrap(WaitAndRetryPolicy, CircuitBreakerPolicy, TimeoutPolicy);
            CommonResilienceWrapPolicyAsync = Policy.WrapAsync(WaitAndRetryPolicyAsync, CircuitBreakerPolicyAsync, TimeoutPolicyAsync);

        }
    }
}
