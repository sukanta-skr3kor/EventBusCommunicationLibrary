//*********************************************************************************************
//* File             :   Program.cs
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
using Sukanta.EventBus.Abstraction.SubscriptionManager;
using Sukanta.EventBus.AzureStorageQueue;
using DemoConsumer;
using DemoEventsAndHandlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace DemoPublisher
{
    sealed class Program
    {
        private Program()
        { }

        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
             .UseWindowsService()
             .ConfigureServices((hostContext, services) =>
             {
                 Log.Logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                 .WriteTo.Console()
                //.WriteTo.File("log.txt")
                .CreateLogger();

                 //InMemory Queue
                 //services.AddSingleton<IEventBus, InMemoryQueueEventBus>(serviceProvider =>
                 //{
                 //    var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

                 //    var eventBusSubcriptionsManager = serviceProvider.GetRequiredService<IEventBusSubscriptionManager>();
                 //    return new InMemoryQueueEventBus(10000, eventBusSubcriptionsManager, scopeFactory, Log.Logger);
                 //});

                 //RabbitMQ      
                 //services.AddSingleton<IRabbitMQConnection>(serviceProvider =>
                 //{
                 //    return new RabbitMQConnection(Log.Logger);
                 //});

                 //services.AddSingleton<IEventBus, RabbitMQEventBus>(serviceProvider =>
                 //{
                 //    var rabbitMQPersistentConnection = serviceProvider.GetRequiredService<IRabbitMQConnection>();
                 //    var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

                 //    var eventBusSubcriptionsManager = serviceProvider.GetRequiredService<IEventBusSubscriptionManager>();
                 //    return new RabbitMQEventBus(rabbitMQPersistentConnection, eventBusSubcriptionsManager, scopeFactory, Log.Logger);
                 //});

                 // Azure Storage Queue
                 services.AddSingleton<IAzureStorageQueueConnection>(serviceProvider =>
                 {
                     string storageConnectionString = "DefaultEndpointsProtocol=https;AccountName=sukantatest2566474934;AccountKey=J3UhQ/CMOn4MGCFJnh0NM6fY91WVegw6gWXupDaUFF3wUPWGTJz8t1X8afD8WIifnd5gMh8McGr0Xt+kTfIzNQ==;EndpointSuffix=core.windows.net";
                     return new AzureStorageQueueConnection(storageConnectionString, Log.Logger);
                 });

                 services.AddSingleton<IEventBus, AzureStorageQueueEventBus>(serviceProvider =>
                 {
                     var storageQueueConnection = serviceProvider.GetRequiredService<IAzureStorageQueueConnection>();
                     var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

                     var eventBusSubcriptionsManager = serviceProvider.GetRequiredService<IEventBusSubscriptionManager>();
                     return new AzureStorageQueueEventBus(storageQueueConnection, eventBusSubcriptionsManager, scopeFactory, Log.Logger);
                 });

                 //Kafka
                 //services.AddSingleton<IKafkaConnection>(serviceProvider =>
                 //{
                 //    return new KafkaConnection(Log.Logger);
                 //});

                 //services.AddSingleton<IEventBus, KafkaEventBus>(serviceProvider =>
                 //{
                 //    var kafkaConnection = serviceProvider.GetRequiredService<IKafkaConnection>();
                 //    var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

                 //    var eventBusSubcriptionsManager = serviceProvider.GetRequiredService<IEventBusSubscriptionManager>();
                 //    return new KafkaEventBus(kafkaConnection, eventBusSubcriptionsManager, scopeFactory, Log.Logger);
                 //});

                 services.AddSingleton<IEventBusSubscriptionManager, EventBusSubscriptionManager>();

                 //Hosted services
                 services.AddHostedService<ConsumerService>();

                 //handler
                 services.AddSingleton<EventHandlerOne>();
                 services.AddSingleton<EventHandlerTwo>();
                 services.AddSingleton<EventHandlerCommon>();

                 //Subscribe
                 var eventBus = services.BuildServiceProvider().GetRequiredService<IEventBus>();

                 //eventBus.Subscribe<EventOne, EventHandlerOne>();
                 eventBus.Subscribe<EventOne, EventHandlerOne>("queueone");
                 //eventBus.Subscribe<EventOne, EventHandlerOne>("queuetwo");

                 //eventBus.Subscribe<EventOne, EventHandlerCommon>("queuetwo");
                 //eventBus.Subscribe<EventTwo, EventHandlerCommon>("queuetwo");

                 //eventBus.Subscribe<EventTwo, EventHandlerTwo>("queuetwo");
             });
    }
}
