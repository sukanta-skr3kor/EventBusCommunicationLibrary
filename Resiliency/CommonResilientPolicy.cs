//*********************************************************************************************
//* File             :   CommonResilientPolicy.cs
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
using Serilog;
using System;

namespace Sukanta.Resiliency
{
    public class CommonResilientPolicy : ResilientPolicy
    {
        //Policy data
        public override string PolicyName { get; set; } = "CommonPolicy";
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
        /// CommonResilientPolicy
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="breakDuration"></param>
        /// <param name="retryCount"></param>
        /// <param name="timeOut"></param>
        public CommonResilientPolicy(ILogger logger, int breakDuration = 1, int retryCount = 3, int timeOut = 5)
        {
            TimeoutValue = timeOut;
            RetryCount = retryCount;
            BreakDuration = breakDuration;
            Logger = logger;

            TimeoutPolicy = Policy.Timeout(timeOut, TimeoutStrategy.Optimistic);
            TimeoutPolicyAsync = Policy.TimeoutAsync(timeOut, TimeoutStrategy.Optimistic);

            RetryPolicy = Policy.Handle<Exception>().Retry(retryCount);
            RetryPolicyAsync = Policy.Handle<Exception>().RetryAsync(retryCount);

            WaitAndRetryPolicy = Policy.Handle<Exception>()
                .WaitAndRetry(retryCount, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), (ex, time) =>
                {
                    Logger.Error(ex, "Could not succeed after {TimeOut}s ({ExceptionMessage})", $"{time.TotalSeconds:n1}", ex.Message);
                });
            WaitAndRetryPolicyAsync = Policy.Handle<Exception>()
               .WaitAndRetryAsync(retryCount, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), (ex, time) =>
               {
                   Logger.Error(ex, "Could not succeed after {TimeOut}s ({ExceptionMessage})", $"{time.TotalSeconds:n1}", ex.Message);
               });

            CircuitBreakerPolicy = Policy.Handle<Exception>().CircuitBreaker(retryCount, TimeSpan.FromMilliseconds(BreakDuration));
            CircuitBreakerPolicyAsync = Policy.Handle<Exception>().CircuitBreakerAsync(retryCount, TimeSpan.FromMilliseconds(BreakDuration));


            CommonResilienceWrapPolicy = Policy.Wrap(RetryPolicy, WaitAndRetryPolicy, CircuitBreakerPolicy, TimeoutPolicy);
            CommonResilienceWrapPolicyAsync = Policy.WrapAsync(RetryPolicyAsync, WaitAndRetryPolicyAsync, CircuitBreakerPolicyAsync, TimeoutPolicyAsync);
        }
    }
}
