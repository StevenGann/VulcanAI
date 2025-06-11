# VulcanAI

VulcanAI is a powerful, modular toolkit designed for building AI-powered applications. Whether you're creating data analyzers, chatbots, or complex AI agents, VulcanAI provides the tools and framework you need to bring your AI applications to life.

## Features

- **Modular Architecture**: Build applications by combining different AI components
- **Versatile Applications**: Support for various AI use cases including:
  - Data analysis and processing
  - Chatbot development
  - Complex AI agent systems
- **Extensible Framework**: Easy to extend and customize for specific needs


## Project Structure

- `VulcanAI/`: Core library containing the main framework components, interface definitions, top-level logic, and some basic implementations. Released builds can be found on NuGet: [VulcanAI](https://www.nuget.org/packages/VulcanAI/)
- `VulcanAI.Discord/`: Contains all Discord-related functionality and integration for building AI-powered Discord bots and applications. Released builds can be found on NuGet: [VulcanAI.Discord](https://www.nuget.org/packages/VulcanAI.Discord/)
- `VulcanAI.Obsidian/`: Contains all Obsidian-related functionality. This package has not been published yet because it depends on ObsidianDB, which is not yet available on NuGet.
- `VulcanAI.Demo/`: Example applications demonstrating various use cases

### Submodules

This project uses Git submodules for external dependencies. After cloning the repository, you'll need to initialize and update the submodules:

```bash
# Initialize and update all submodules (including nested ones)
git submodule update --init --recursive

# Update all submodules to their latest commits
git submodule update --recursive --remote

# Pull latest changes for all submodules
git submodule foreach git pull
```

Current submodules:
- [ObsidianDB](https://github.com/StevenGann/ObsidianDB): A powerful C# library for programmatically managing Obsidian vaults
- [HyperVectorDB](https://github.com/StevenGann/HyperVectorDB): A local vector database built in C# that supports various distance/similarity measures

#### Submodule Management

When working with submodules, here are some common commands you might need:

```bash
# Add a new submodule
git submodule add <repository-url> <path>

# Remove a submodule
git submodule deinit <path>
git rm <path>
rm -rf .git/modules/<path>

# Update a specific submodule
cd <submodule-path>
git pull
cd ..
git add <submodule-path>
git commit -m "Update submodule <name>"
```

Note: Some submodules may have their own nested submodules. The `--recursive` flag ensures that all nested submodules are properly initialized and updated.

## Usage

### Using VulcanAI (Core)

Install the package:
```sh
dotnet add package VulcanAI
```

Create a configuration file `agent-config.json`:
```json
{
    "SystemPrompt": "You are VulcanAI, a helpful digital assistant. Your primary function is to demonstrate the features and capability of the VulcanAI library. You are friendly, helpful, and informative. You are also a bit sarcastic and witty.",
    "Name": "VulcanAI"
}
```

Minimal example of running a console agent:
```csharp
using Microsoft.Extensions.Logging;
using VulcanAI.Agent;
using VulcanAI.LLM;
using VulcanAI.Connectors;

var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.SetMinimumLevel(LogLevel.Error).AddConsole();
});
var logger = loggerFactory.CreateLogger<Program>();
var llmLogger = loggerFactory.CreateLogger<LocalLLMClient>();
var httpClient = new HttpClient();
var llmClient = new LocalLLMClient(new HttpClientWrapper(httpClient), "local-model", "http://localhost:1234", llmLogger, useOpenAIFormat: false);
llmClient.MaxPromptLength = 6000;
var agentConfig = new AgentConfig { SystemPrompt = "You are VulcanAI, a helpful digital assistant.", Name = "VulcanAI" };
var consoleLogger = loggerFactory.CreateLogger<ConsoleConnector>();
var messageInterface = new ConsoleConnector(consoleLogger);
var agentLogger = loggerFactory.CreateLogger<Chatbot>();
var agent = new Chatbot(llmClient, agentLogger, agentConfig, messageInterface);
await messageInterface.StartAsync();
```

### Using VulcanAI.Discord

Install the package:
```sh
dotnet add package VulcanAI.Discord
```

Add your Discord configuration to `secrets.json`:
```json
{
    "Discord": {
        "Token": "your_discord_bot_token",
        "ChannelId": 123456789012345678
    }
}
```

Minimal example of running a Discord bot:
```csharp
using Microsoft.Extensions.Logging;
using VulcanAI.Agent;
using VulcanAI.LLM;
using VulcanAI.Connectors;
using Discord.WebSocket;

var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.SetMinimumLevel(LogLevel.Debug).AddConsole();
});
var logger = loggerFactory.CreateLogger<Program>();
var llmLogger = loggerFactory.CreateLogger<LocalLLMClient>();
var httpClient = new HttpClient();
var llmClient = new LocalLLMClient(new HttpClientWrapper(httpClient), "local-model", "http://localhost:1234", llmLogger, useOpenAIFormat: false);
llmClient.MaxPromptLength = 6000;
var agentConfig = new AgentConfig { SystemPrompt = "You are VulcanAI, a helpful digital assistant.", Name = "VulcanAI" };
var discordLogger = loggerFactory.CreateLogger<DiscordConnector>();
var socketConfig = new DiscordSocketConfig { GatewayIntents = GatewayIntents.All };
var discordClient = new DiscordSocketClient(socketConfig);
var discordConfig = new DiscordConfig { Token = "your_discord_bot_token", ChannelId = 123456789012345678 };
var messageInterface = new DiscordConnector(discordClient, discordLogger, discordConfig.Token, discordConfig.ChannelId);
var agentLogger = loggerFactory.CreateLogger<Chatbot>();
var agent = new Chatbot(llmClient, agentLogger, agentConfig, messageInterface);
await messageInterface.StartAsync();
```

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.