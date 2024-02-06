//*********************************************************************************************
//* File             :   SqlResilientPolicy.cs
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
using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.Retry;
using Polly.Timeout;
using Polly.Wrap;
using Serilog;
using System;
using System.Data.SqlClient;
using System.Threading.Tasks;

namespace Sukanta.Resiliency
{
    /// <summary>
    /// SQL resiliency policy
    /// </summary>
    public partial class SqlResilientPolicy : ResilientPolicy
    {
        //Policy data
        public override string PolicyName { get; set; } = "SqlPolicy";
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
        /// SqlResilientPolicy
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="breakDuration"></param>
        /// <param name="retryCount"></param>
        /// <param name="timeOut"></param>
        public SqlResilientPolicy(ILogger logger, int breakDuration = 1, int retryCount = 3, int timeOut = 5)
        {
            TimeoutValue = timeOut;
            RetryCount = retryCount;
            BreakDuration = breakDuration;
            Logger = logger;

            TimeoutPolicy = Policy.Timeout(timeOut, TimeoutStrategy.Optimistic);
            TimeoutPolicyAsync = Policy.TimeoutAsync(timeOut, TimeoutStrategy.Optimistic);


            RetryPolicy = Policy.Handle<SqlException>().Retry(retryCount);
            RetryPolicyAsync = Policy.Handle<SqlException>().RetryAsync(retryCount);


            WaitAndRetryPolicy = Policy.Handle<SqlException>()
                .WaitAndRetry(retryCount, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), (ex, time) =>
                {
                    Logger.Error(ex, "Could not perform SQL operation after {TimeOut}s ({ExceptionMessage})", $"{time.TotalSeconds:n1}", ex.Message);
                });

            WaitAndRetryPolicyAsync = Policy.Handle<SqlException>()
                .WaitAndRetryAsync(retryCount, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), (ex, time) =>
                {
                    Logger.Error(ex, "Could not perform SQL operation after {TimeOut}s ({ExceptionMessage})", $"{time.TotalSeconds:n1}", ex.Message);
                });

            CircuitBreakerPolicy = Policy.Handle<SqlException>()
                           .CircuitBreaker(retryCount, TimeSpan.FromMilliseconds(BreakDuration));
            CircuitBreakerPolicyAsync = Policy.Handle<SqlException>()
                                 .CircuitBreakerAsync(retryCount, TimeSpan.FromMilliseconds(BreakDuration));

            CommonResilienceWrapPolicy = Policy.Wrap(WaitAndRetryPolicy, CircuitBreakerPolicy, TimeoutPolicy);
            CommonResilienceWrapPolicyAsync = Policy.WrapAsync(WaitAndRetryPolicyAsync, CircuitBreakerPolicyAsync, TimeoutPolicyAsync);
        }
    }

    /// <summary>
    /// Handles SQL Transactions
    /// </summary>
    public partial class SqlResilientPolicy
    {
        private DbContext _dbContext;
        private SqlResilientPolicy(DbContext dbContext) =>
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

        public static SqlResilientPolicy New(DbContext context) =>
            new SqlResilientPolicy(context);

        /// <summary>
        /// Executes a database transaction
        /// </summary>
        /// <param name="action"></param>
        /// <returns></returns>
        public async Task ExecuteAsync(Func<Task> action)
        {
            var strategy = _dbContext.Database.CreateExecutionStrategy();

            await strategy.ExecuteAsync(async () =>
            {
                using (var transaction = _dbContext.Database.BeginTransaction())
                {
                    await action();
                    transaction.Commit();
                }
            });
        }
    }
}
