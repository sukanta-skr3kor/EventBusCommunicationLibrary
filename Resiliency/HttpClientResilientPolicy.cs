//*********************************************************************************************
//* File             :   HttpClientResilientPolicy.cs
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
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;
using Polly.Wrap;
using Serilog;
using System;
using System.Net;
using System.Net.Http;

namespace Sukanta.Resiliency
{
    /// <summary>
    ///HTTP Client(Http request) resiliency policy
    /// </summary>
    public class HttpClientResilientPolicy : ResilientPolicy
    {
        //Policy data
        public override string PolicyName { get; set; } = "HttpPolicy";
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


        //Additional Policies for HTTP call
        public readonly AsyncRetryPolicy<HttpResponseMessage> OnHttpResponseRetryPolicyAsync;

        public readonly AsyncRetryPolicy<HttpResponseMessage> OnHttpResponseWaitAndRetryPolicyAsync;

        public readonly AsyncCircuitBreakerPolicy<HttpResponseMessage> OnHttpResponseCircuitBreakerPolicyAsync;

        public readonly AsyncPolicyWrap<HttpResponseMessage> OnHttpResponseCommonResilienceWrapPolicyAsync;


        /// <summary>
        ///HttpClientResilientPolicy
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="retryCount"></param>
        /// <param name="breakDuration"></param>
        /// <param name="timeOut"></param>
        public HttpClientResilientPolicy(ILogger logger, int retryCount = 3, int breakDuration = 1, int timeOut = 5)
        {
            RetryCount = retryCount;
            TimeoutValue = timeOut;
            BreakDuration = breakDuration;
            Logger = logger;

            TimeoutPolicy = Policy.Timeout(timeOut, TimeoutStrategy.Optimistic);
            TimeoutPolicyAsync = Policy.TimeoutAsync(timeOut, TimeoutStrategy.Optimistic);

            RetryPolicy = Policy.Handle<HttpRequestException>().Retry(retryCount);
            RetryPolicyAsync = Policy.Handle<HttpRequestException>().RetryAsync(retryCount);


            WaitAndRetryPolicy = Policy.Handle<HttpRequestException>()
                                 .Or<Exception>()
                                 .WaitAndRetry(retryCount, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), (ex, time) =>
                                   {
                                       Logger.Error(ex, "Could not perform Http Client operation after {TimeOut}s ({ExceptionMessage})", $"{time.TotalSeconds:n1}", ex.Message);
                                   });
            WaitAndRetryPolicyAsync = Policy.Handle<HttpRequestException>()
                                .Or<Exception>()
                                .WaitAndRetryAsync(retryCount, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), (ex, time) =>
                                {
                                    Logger.Error(ex, "Could not perform Http Client operation after {TimeOut}s ({ExceptionMessage})", $"{time.TotalSeconds:n1}", ex.Message);
                                });

            CircuitBreakerPolicy = Policy.Handle<HttpRequestException>()
                                     .Or<Exception>()
                                     .CircuitBreaker(retryCount, TimeSpan.FromSeconds(BreakDuration));
            CircuitBreakerPolicyAsync = Policy.Handle<HttpRequestException>()
                                   .Or<Exception>()
                                   .CircuitBreakerAsync(retryCount, TimeSpan.FromSeconds(BreakDuration));


            OnHttpResponseCircuitBreakerPolicyAsync = Policy.HandleResult<HttpResponseMessage>(x => x.StatusCode == HttpStatusCode.InternalServerError
                                              || x.StatusCode == HttpStatusCode.ServiceUnavailable || x.StatusCode == HttpStatusCode.RequestTimeout)
                                                .CircuitBreakerAsync(retryCount, TimeSpan.FromSeconds(BreakDuration),
                                                           (result, BreakDuration) =>
                                                           {
                                                               OnBreak(result, BreakDuration, retryCount, logger);
                                                           },
                                                           () =>
                                                           {
                                                               OnReset(logger);
                                                           });

            OnHttpResponseRetryPolicyAsync = Policy.Handle<HttpRequestException>()
                  .OrResult<HttpResponseMessage>(x => x.StatusCode != HttpStatusCode.OK).RetryAsync(retryCount);


            OnHttpResponseWaitAndRetryPolicyAsync = Policy.HandleResult<HttpResponseMessage>(x => x.StatusCode != HttpStatusCode.OK)
               .WaitAndRetryAsync(retryCount, i => TimeSpan.FromSeconds(2), (result, timeSpan, retryCounter, context) =>
               {
                   logger.Warning($"Request failed with {result.Result.StatusCode}. Waiting {timeSpan} before next retry. Retry attempt {retryCounter}");
               });

            CommonResilienceWrapPolicy = Policy.Wrap(WaitAndRetryPolicy, CircuitBreakerPolicy, TimeoutPolicy);
            CommonResilienceWrapPolicyAsync = Policy.WrapAsync(WaitAndRetryPolicyAsync, CircuitBreakerPolicyAsync, TimeoutPolicyAsync);
            OnHttpResponseCommonResilienceWrapPolicyAsync = Policy.WrapAsync(OnHttpResponseWaitAndRetryPolicyAsync, OnHttpResponseCircuitBreakerPolicyAsync);
        }

        /// <summary>
        /// On Break of circuit
        /// </summary>
        /// <param name="result"></param>
        /// <param name="breakDuration"></param>
        /// <param name="retryCount"></param>
        /// <param name="logger"></param>
        public static void OnBreak(DelegateResult<HttpResponseMessage> result, TimeSpan breakDuration, int retryCount, ILogger logger)
        {
            logger.Warning("Target service shutdown during {breakDuration} after {DefaultRetryCount} failed retries.", breakDuration, retryCount);
            throw new BrokenCircuitException("Service unavailable. Please try again later");
        }

        /// <summary>
        /// On restart , circuit closed
        /// </summary>
        /// <param name="logger"></param>
        public static void OnReset(ILogger logger)
        {
            logger.Information("Target service restarted and running now.");
        }
    }
}