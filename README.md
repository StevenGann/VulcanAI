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

[Usage examples and documentation will be added here]

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.