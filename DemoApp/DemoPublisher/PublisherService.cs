//*********************************************************************************************
//* File             :   PublisherService.cs
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
using System.Threading;
using System.Threading.Tasks;

namespace DemoPublisher
{
    public class PublisherService : BackgroundService
    {
        private readonly IEventBus _eventBus;
        public PublisherService(IEventBus eventBus)
        {
            _eventBus = eventBus;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            int count = 1;
            do
            {
                try
                {
                    EventOne eventOne = new EventOne();
                    eventOne.data = count++.ToString();
                    _eventBus.Publish(eventOne);
                    await Task.Delay(1000);
                }
                catch { }
            } while (true);
        }

    }
}

