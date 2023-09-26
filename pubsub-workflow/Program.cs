﻿// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using Dapr.Client;
using Dapr.Tests.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PubsubWorkflow
{
    
    class PubsubWorkflow
    {
        private static string rapidPubsubName = "longhaul-sb-rapid";
        private static string mediumPubsubName = "longhaul-sb-medium";
        private static string slowPubsubName = "longhaul-sb-slow";
        private static string glacialPubsubName = "longhaul-sb-glacial";

        static void Main(string[] args)
        {
            ObservabilityUtils.StartMetricsServer();

            var host = CreateHostBuilder(args).Build();

            var logger = host.Services.GetRequiredService<ILogger<PubsubWorkflow>>();

            // When ran in k8s, this app might start before Dapr sidecar is ready
            // and this will lead to errors. Let's wait a bit for the sidecar to
            // be ready before we start publishing.
            logger.LogInformation("Waiting for Dapr sidecar to be ready...");
            CancellationToken timeout = new CancellationTokenSource(TimeSpan.FromMinutes(1)).Token;
            var client = new DaprClientBuilder().Build();
            client.WaitForSidecarAsync(timeout).Wait();
            
            logger.LogInformation("Starting Pubsub Workflow");

            var rapidTimer = StartPublishingMessages(10, rapidPubsubName, "rapidtopic");
            var mediumTimer = StartPublishingMessages(300, mediumPubsubName, "mediumtopic");
            var slowTimer = StartPublishingMessages(3600, slowPubsubName, "slowtopic");
            var glacialTimer = StartPublishingMessages(3600*12, glacialPubsubName, "glacialtopic");
            
            host.Run();

            logger.LogInformation("Exiting Pubsub Workflow");

            rapidTimer.Dispose();
            mediumTimer.Dispose();
            slowTimer.Dispose();
            glacialTimer.Dispose();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureTestInfraLogging()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    var appSettings = new ConfigurationBuilder()
                        .SetBasePath(Directory.GetCurrentDirectory())
                        .AddJsonFile($"appsettings.json", optional: true, reloadOnChange: true)                        
                        .AddCommandLine(args)
                        .Build();

                    webBuilder.UseStartup<Startup>()
                        .UseUrls(urls: $"http://*:{appSettings["DaprHTTPAppPort"]}");
                });

        static internal Timer StartPublishingMessages(int periodInSeconds, string pubsubName, string topic)
        {
            var client = new DaprClientBuilder().Build();
            var messagePublisher = new MessagePublisher(client, pubsubName, topic);

            return new Timer(messagePublisher.Publish, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(periodInSeconds));
        }
    }
}
