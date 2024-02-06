//*********************************************************************************************
//* File             :   ResilientPolicy.cs
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
    /// ResilientPolicy Abstraction
    /// </summary>
    public abstract class ResilientPolicy : IResilientPolicy
    {
        public abstract RetryPolicy RetryPolicy { get; set; }
        public abstract Policy WaitAndRetryPolicy { get; set; }

        public virtual PolicyWrap CommonResilienceWrapPolicy { get; set; }
        public virtual Policy TimeoutPolicy { get; set; }
        public virtual Policy CircuitBreakerPolicy { get; set; }


        //Async policies
        public abstract AsyncPolicy WaitAndRetryPolicyAsync { get; set; }
        public abstract AsyncRetryPolicy RetryPolicyAsync { get; set; }

        public virtual AsyncPolicy TimeoutPolicyAsync { get; set; }
        public virtual AsyncPolicy CircuitBreakerPolicyAsync { get; set; }
        public virtual AsyncPolicyWrap CommonResilienceWrapPolicyAsync { get; set; }


        //Policy data
        public abstract int RetryCount { get; set; }
        public abstract int TimeoutValue { get; set; }

        public abstract int BreakDuration { get; set; }
        public abstract string PolicyName { get; set; }

        //Logger
        public abstract ILogger Logger { get; set; }
        /// <summary>
        /// ResilientPolicy
        /// </summary>
        /// <param name="breakDuration"></param>
        /// <param name="retryCount"></param>
        /// <param name="timeOut"></param>
        protected ResilientPolicy(int breakDuration = 1, int retryCount = 3, int timeOut = 5)
        {
            RetryCount = retryCount;
            TimeoutValue = timeOut;
            BreakDuration = breakDuration;
        }
    }
}
