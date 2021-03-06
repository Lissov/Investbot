﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Linq;
using Hangfire;
using Investbot.BusinessLogic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.BotFramework;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Configuration;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Investbot
{
    /// <summary>
    /// The Startup class configures services and the request pipeline.
    /// </summary>
    public class Startup
    {
        private ILoggerFactory loggerFactory;
        private bool isProduction;

        public Startup(IHostingEnvironment env)
        {
            isProduction = env.IsProduction();

            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();

            Configuration = builder.Build();
        }

        /// <summary>
        /// Gets the configuration that represents a set of key/value application configuration properties.
        /// </summary>
        /// <value>
        /// The <see cref="IConfiguration"/> that represents a set of key/value application configuration properties.
        /// </value>
        public IConfiguration Configuration { get; }

        /// <summary>
        /// This method gets called by the runtime. Use this method to add services to the container.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> specifies the contract for a collection of service descriptors.</param>
        /// <seealso cref="IStatePropertyAccessor{T}"/>
        /// <seealso cref="https://docs.microsoft.com/en-us/aspnet/web-api/overview/advanced/dependency-injection"/>
        /// <seealso cref="https://docs.microsoft.com/en-us/azure/bot-service/bot-service-manage-channels?view=azure-bot-service-4.0"/>
        public void ConfigureServices(IServiceCollection services)
        {
            var secretKey = Configuration.GetSection("botFileSecret")?.Value;
            var botFilePath = Configuration.GetSection("botFilePath")?.Value;

            // Loads .bot configuration file and adds a singleton that your Bot can access through dependency injection.
            var botConfig = BotConfiguration.Load(botFilePath, secretKey);
            services.AddSingleton(sp => botConfig);

            // Add BotServices singleton.
            // Create the connected services from .bot file.
            services.AddSingleton(sp => new BotServices(botConfig));

            var environment = isProduction ? "production" : "development";
            var service = botConfig.Services.FirstOrDefault(s => s.Type == "endpoint" && s.Name == environment);
            if (!(service is EndpointService endpointService))
            {
                throw new InvalidOperationException($"The .bot file does not contain an endpoint with name '{environment}'.");
            }

            //IStorage dataStore = new MemoryStorage();
            // For production bots use the Azure Blob or
            // Azure CosmosDB storage providers. For the Azure
            // based storage providers, add the Microsoft.Bot.Builder.Azure
            // Nuget package to your solution. That package is found at:
            // https://www.nuget.org/packages/Microsoft.Bot.Builder.Azure/
            // Un-comment the following lines to use Azure Blob Storage
            // Storage configuration name or ID from the .bot file.
            const string StorageConfigurationId = "blobstorage";
            var blobConfig = botConfig.FindServiceByNameOrId(StorageConfigurationId);
            if (!(blobConfig is BlobStorageService blobStorageConfig))
            {
               throw new InvalidOperationException($"The .bot file does not contain an blob storage with name '{StorageConfigurationId}'.");
            }
            // Default container name.
            const string DefaultBotContainer = "botstate";
            var storageContainer = string.IsNullOrWhiteSpace(blobStorageConfig.Container) ? DefaultBotContainer : blobStorageConfig.Container;
            IStorage dataStore = new Microsoft.Bot.Builder.Azure.AzureBlobStorage(blobStorageConfig.ConnectionString, storageContainer);

            var conversationState = new ConversationState(dataStore);
            services.AddSingleton(conversationState);
            var userState = new UserState(dataStore);
            services.AddSingleton(userState);

            var iservice = botConfig.Services.FirstOrDefault(s => s.Type == "endpoint" && s.Name == "investservice-" + environment);
            if (!(iservice is EndpointService investServiceEndpoint))
            {
                throw new InvalidOperationException($"The .bot file does not contain an investment endpoint with name '{environment}'.");
            }

            var investService = new InvestDataService(investServiceEndpoint.Endpoint, investServiceEndpoint.AppId, investServiceEndpoint.AppPassword);
            services.AddSingleton(investService);
            var botAccount = new MicrosoftAppCredentials(endpointService.AppId, endpointService.AppPassword);
            var pushService = new PortfolioPushService(investService, botAccount, dataStore);
            services.AddSingleton(pushService);

            services.AddBot<InvestbotBot> (options =>
            {
                options.CredentialProvider = new SimpleCredentialProvider(endpointService.AppId, endpointService.AppPassword);
                options.ChannelProvider = new ConfigurationChannelProvider(Configuration);
                ILogger logger = loggerFactory.CreateLogger<InvestbotBot>();

                // Catches any errors that occur during a conversation turn and logs them.
                options.OnTurnError = async (context, exception) =>
                {
                    logger.LogError($"Exception caught : {exception}");
                    await context.SendActivityAsync("Sorry, it looks like something went wrong.");
                };              
            });

            var connectionString =
                "Server=tcp:iwnp0c751k.database.windows.net,1433;Initial Catalog=invest_hangfire;Persist Security Info=False;User ID=Investbot;Password=InvBotPDollarwyrdSharp_;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;";
            try
            {
                services.AddHangfire(conf => conf.UseSqlServerStorage(connectionString));
                //pushService.SetupJob();
            }
            catch (Exception ex)
            {
                Diagnostic.Log += ex.Message;
            }
        }

        public void Configure(IApplicationBuilder app, ILoggerFactory loggerFactory, IHostingEnvironment env)
        {
            this.loggerFactory = loggerFactory;

            try {
                app.UseHangfireDashboard();
                app.UseHangfireServer();
            }
            catch (Exception ex)
            {
                Diagnostic.Log += ex.Message;
            }

            app.UseDefaultFiles()
                .UseStaticFiles()
                .UseBotFramework();
        }
    }
}
