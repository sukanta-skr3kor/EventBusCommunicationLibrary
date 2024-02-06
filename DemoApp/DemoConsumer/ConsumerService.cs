//*********************************************************************************************
//* File             :   ConsumerService.cs
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
using Sukanta.EventBus.Abstraction.Bus;
using DemoEventsAndHandlers;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DemoConsumer
{
    public class ConsumerService : BackgroundService
    {
        private readonly IEventBus _eventBus;
        //private readonly IRabbitMQConnection _rabbitMQConnection;
        //private ErrorEventHandler _errorEventHandler;

        public ConsumerService(IEventBus eventBus)
        {
            _eventBus = eventBus;
        }



        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            int countOne = 1;
            int countTwo = 1001;

            do
            {
                try
                {
                    EventOne eventOne = new EventOne();
                    eventOne.data = countOne++.ToString();
                    //_eventBus.Publish(eventOne);//Publish to default queue
                    //_eventBus.Publish(eventOne, "queuecommon");
                    //_eventBus.Publish(eventOne, "queueone");

                    //eventOne.data = countOne++.ToString();
                    _eventBus.Publish(eventOne, "queueone");


                    // EventTwo eventTwo = new EventTwo();
                    // eventTwo.data = countTwo++.ToString();
                    //_eventBus.Publish(eventTwo, "queuetwo");

                    await Task.Delay(2000);
                }
                catch (Exception exp)
                {
                    Console.WriteLine(exp.Message);
                }
            } while (true);

        }
    }
}

