using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using VulcanAI.Agent;
using VulcanAI.LLM;
using VulcanAI.Configuration;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Threading;
using VulcanAI.Infrastructure.Discord;
using VulcanAI.Connectors;
using VulcanAI.Agent;
using Discord.WebSocket;
using System.Net.Http;
using Discord;

namespace VulcanAI.Demo
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            // Create logger factory with debug level
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .SetMinimumLevel(LogLevel.Debug)  // Set minimum level to Debug
                    .AddConsole();                    // Add console logging
            });

            var logger = loggerFactory.CreateLogger<Program>();
            logger.LogInformation("Starting VulcanAI demo application");

            try
            {
                // Load configuration
                var config = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("secrets.json", optional: false)
                    .AddJsonFile("agent-config.json", optional: false)
                    .Build();

                var agentConfig = config.Get<AgentConfig>();
                if (agentConfig == null || string.IsNullOrEmpty(agentConfig.SystemPrompt) || string.IsNullOrEmpty(agentConfig.Name))
                {
                    logger.LogError("Invalid agent configuration. Please check agent-config.json");
                    logger.LogInformation("Sample configuration: {SampleConfig}", @"
{
    ""SystemPrompt"": ""You are a helpful AI assistant. Respond to user messages in a friendly and informative way."",
    ""Name"": ""AI Assistant""
}");
                    return;
                }

                // Create LLM client
                var llmLogger = loggerFactory.CreateLogger<LocalLLMClient>();
                var httpClient = new HttpClient();
                var httpClientWrapper = new HttpClientWrapper(httpClient);
                var llmClient = new LocalLLMClient(
                    httpClientWrapper,
                    "local-model",  // Model name
                    "http://localhost:1234",  // Base URL
                    llmLogger,
                    useOpenAIFormat: false);  // Use LM Studio format

                llmClient.MaxPromptLength = 6000;

                // Choose interface type
                IMessageConnector messageInterface;
                Console.WriteLine("Choose interface type:");
                Console.WriteLine("1. Discord");
                Console.WriteLine("2. Console");
                Console.Write("Enter your choice (1 or 2): ");
                
                var choice = Console.ReadLine();
                switch (choice)
                {
                    case "1":
                        var discordConfig = config.GetSection("Discord").Get<Configuration.DiscordConfig>();
                        if (discordConfig == null || string.IsNullOrEmpty(discordConfig.Token) || discordConfig.ChannelId == 0)
                        {
                            logger.LogError("Invalid Discord configuration. Please check secrets.json");
                            logger.LogInformation("Sample configuration: {SampleConfig}", @"
{
    ""Discord"": {
        ""Token"": ""your_discord_bot_token"",
        ""ChannelId"": 123456789012345678
    }
}");
                            return;
                        }

                        var discordLogger = loggerFactory.CreateLogger<VulcanAI.Infrastructure.Discord.DiscordInterface>();
                        var socketConfig = new DiscordSocketConfig
                        {
                            GatewayIntents = GatewayIntents.All
                        };
                        var discordClient = new DiscordSocketClient(socketConfig);
                        messageInterface = new VulcanAI.Infrastructure.Discord.DiscordInterface(
                            discordClient,
                            discordLogger,
                            discordConfig.Token,
                            discordConfig.ChannelId);
                        break;

                    case "2":
                        // For console interface, set log level to Error to avoid cluttering the console
                        loggerFactory = LoggerFactory.Create(builder =>
                        {
                            builder
                                .SetMinimumLevel(LogLevel.Error)  // Set minimum level to Error
                                .AddConsole();                    // Add console logging
                        });
                        logger = loggerFactory.CreateLogger<Program>();
                        var consoleLogger = loggerFactory.CreateLogger<ConsoleConnector>();
                        messageInterface = new ConsoleConnector(consoleLogger);
                        break;

                    default:
                        logger.LogError("Invalid choice. Please enter 1 or 2.");
                        return;
                }

                // Create agent
                var agentLogger = loggerFactory.CreateLogger<Agent.Agent>();
                var agent = new Agent.Agent(
                    llmClient,
                    agentLogger,
                    agentConfig,
                    messageInterface);

                // Start the chosen interface
                await messageInterface.StartAsync();

                // Handle graceful shutdown
                var cts = new CancellationTokenSource();
                Console.CancelKeyPress += (s, e) =>
                {
                    e.Cancel = true;
                    cts.Cancel();
                };

                logger.LogInformation("VulcanAI demo application started. Press Ctrl+C to exit.");
                await Task.Delay(-1, cts.Token);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error starting AI Actions application");
            }
            finally
            {
                loggerFactory.Dispose();
            }
        }
    }
} 