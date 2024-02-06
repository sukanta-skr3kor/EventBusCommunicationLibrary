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

using DemoEventsAndHandlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Sukanta.EventBus.Abstraction.Bus;
using Sukanta.EventBus.Abstraction.SubscriptionManager;
using Sukanta.EventBus.Mqtt;

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

        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            return Host.CreateDefaultBuilder(args)
             .UseWindowsService().
             ConfigureServices((hostContext, services) =>
             {

                 Log.Logger = new LoggerConfiguration()
                  .Enrich.FromLogContext()
                  .WriteTo.Console()
                  .CreateLogger();

                 // RabbitMQ
                 //services.AddSingleton<IRabbitMQConnection>(serviceProvider =>
                 //{
                 //    return new RabbitMQConnection(Log.Logger);
                 //});

                 //services.AddSingleton<IEventBus, RabbitMQEventBus>(serviceProvider =>
                 //{
                 //    var rabbitMQPersistentConnection = serviceProvider.GetRequiredService<IRabbitMQConnection>();
                 //    var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

                 //    var eventBusSubcriptionsManager = serviceProvider.GetRequiredService<IEventBusSubscriptionManager>();
                 //    return new RabbitMQEventBus(rabbitMQPersistentConnection,  eventBusSubcriptionsManager, scopeFactory, Log.Logger);
                 //});

                 //Mqtt
                 services.AddSingleton<IMqttConnection>(serviceProvider =>
                 {
                     return new MqttConnection(Log.Logger, "127.0.0.1");
                 });

                 services.AddSingleton<IEventBus, MqttEventBus>(serviceProvider =>
                 {
                     var mqttPersistentConnection = serviceProvider.GetRequiredService<IMqttConnection>();
                     var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

                     var eventBusSubcriptionsManager = serviceProvider.GetRequiredService<IEventBusSubscriptionManager>();
                     return new MqttEventBus(mqttPersistentConnection, eventBusSubcriptionsManager, scopeFactory, Log.Logger);
                 });

                 //Redis
                 //services.AddSingleton<IRedisConnection>(serviceProvider =>
                 //{
                 //    return new RedisConnection("localhost:6379");
                 //});

                 //services.AddSingleton<IEventBus, RedisEventBus>(serviceProvider =>
                 //{
                 //    var redisPersistentConnection = serviceProvider.GetRequiredService<IRedisConnection>();
                 //    var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

                 //    var eventBusSubcriptionsManager = serviceProvider.GetRequiredService<IEventBusSubscriptionManager>();
                 //    return new RedisEventBus(redisPersistentConnection, eventBusSubcriptionsManager, scopeFactory, Log.Logger);
                 //});


                 //services.AddSingleton<IAzureStorageQueueConnection>(serviceProvider =>
                 //{
                 //    string storageConnectionString = "DefaultEndpointsProtocol=https;AccountName=sukantatest2566474934;AccountKey=J3UhQ/CMOn4MGCFJnh0NM6fY91WVegw6gWXupDaUFF3wUPWGTJz8t1X8afD8WIifnd5gMh8McGr0Xt+kTfIzNQ==;EndpointSuffix=core.windows.net";

                 //    return new AzureStorageQueueConnection(storageConnectionString, Log.Logger);
                 //});

                 //services.AddSingleton<IEventBus, AzureStorageQueueEventBus>(serviceProvider =>
                 //{
                 //    var storageQueueConnection = serviceProvider.GetRequiredService<IAzureStorageQueueConnection>();
                 //    var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

                 //    var eventBusSubcriptionsManager = serviceProvider.GetRequiredService<IEventBusSubscriptionManager>();
                 //    return new AzureStorageQueueEventBus(storageQueueConnection, eventBusSubcriptionsManager, scopeFactory, Log.Logger);
                 //});

                 services.AddSingleton<IEventBusSubscriptionManager, EventBusSubscriptionManager>();

                 services.AddHostedService<PublisherService>();

                 //handler
                 services.AddScoped<EventHandlerOne>();

                 //Subscribe
                 var eventBus = services.BuildServiceProvider().GetRequiredService<IEventBus>();
                 eventBus.Subscribe<EventOne, EventHandlerOne>();
             });
        }
    }
}
