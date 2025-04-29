using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VulcanAI.LLM;
using VulcanAI.Knowledge;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Threading;
using Microsoft.Extensions.Logging;
using VulcanAI.Connectors;

namespace VulcanAI.Agent;


/// <summary>
/// Represents an AI agent that can process messages and generate responses using a language model.
/// The agent maintains conversation history and context, which is persisted to disk periodically
/// and loaded when the agent is restarted.
/// </summary>
public class Chatbot : IAgent, IDisposable
{
    private readonly ILLMClient _llmClient;
    private readonly ILogger<Chatbot> _logger;
    private readonly IMessageConnector? _messageInterface;
    private readonly string _agentName;
    private readonly AgentContext _context;
    private readonly Timer _contextPersistenceTimer;
    private readonly Timer _periodicMessageTimer;
    private readonly Random _random = new();
    private int _minPeriodicMessageIntervalMinutes = 60;
    private int _maxPeriodicMessageIntervalMinutes = 120;
    private readonly string _contextFilePath;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Gets or sets the minimum interval in minutes for periodic messages.
    /// </summary>
    public int MinPeriodicMessageIntervalMinutes
    {
        get => _minPeriodicMessageIntervalMinutes;
        set
        {
            if (value < 1)
                throw new ArgumentOutOfRangeException(nameof(value), "Minimum interval must be at least 1 minute");
            if (value > _maxPeriodicMessageIntervalMinutes)
                throw new ArgumentOutOfRangeException(nameof(value), "Minimum interval cannot be greater than maximum interval");
            _minPeriodicMessageIntervalMinutes = value;
        }
    }

    /// <summary>
    /// Gets or sets the maximum interval in minutes for periodic messages.
    /// </summary>
    public int MaxPeriodicMessageIntervalMinutes
    {
        get => _maxPeriodicMessageIntervalMinutes;
        set
        {
            if (value < _minPeriodicMessageIntervalMinutes)
                throw new ArgumentOutOfRangeException(nameof(value), "Maximum interval cannot be less than minimum interval");
            _maxPeriodicMessageIntervalMinutes = value;
        }
    }

    /// <summary>
    /// Gets a random interval between MinPeriodicMessageIntervalMinutes and MaxPeriodicMessageIntervalMinutes.
    /// </summary>
    private TimeSpan GetRandomInterval()
    {
        return TimeSpan.FromMinutes(_random.Next(MinPeriodicMessageIntervalMinutes, MaxPeriodicMessageIntervalMinutes + 1));
    }

    /// <summary>
    /// Resets the periodic message timer with a new random interval.
    /// </summary>
    private void ResetPeriodicMessageTimer()
    {
        var interval = GetRandomInterval();
        _periodicMessageTimer.Change(interval, Timeout.InfiniteTimeSpan);
        _logger.LogDebug("Periodic message timer reset to {Interval} minutes", interval.TotalMinutes);
    }

    /// <summary>
    /// Handles the periodic message timer callback.
    /// </summary>
    private async void OnPeriodicMessageTimerCallback()
    {
        try
        {
            // Build the full prompt with context
            var prompt = BuildFullPrompt("Generate a message to share with the users based on our conversation history and context. A joke, fun fact, comments about yourself, or other conversation starter would be a good idea.");
            _logger.LogDebug("Sending periodic message prompt with context: {Prompt}", prompt);

            var response = await _llmClient.GetCompletionAsync(prompt);
            await SendMessageAsync(response);
            
            // Reset the timer for the next message
            ResetPeriodicMessageTimer();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending periodic message");
            // Still reset the timer even if there was an error
            ResetPeriodicMessageTimer();
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Chatbot"/> class with the specified parameters.
    /// </summary>
    /// <param name="llmClient">The language model client to use for generating responses.</param>
    /// <param name="logger">The logger instance for this agent.</param>
    /// <param name="systemPrompt">The system prompt that defines the agent's behavior and personality.</param>
    /// <param name="messageInterface">The message interface for sending and receiving messages.</param>
    /// <param name="agentName">The name of the agent. Used for context persistence and logging.</param>
    /// <param name="contextFields">Optional initial context fields for the agent.</param>
    /// <remarks>
    /// This constructor:
    /// 1. Attempts to load existing context from a JSON file named {agentName}.context.json
    /// 2. Creates a new context if none exists, using the provided system prompt and context fields
    /// 3. Updates the context with any new system prompt or context fields provided
    /// 4. Sets up periodic context persistence (every 5 minutes)
    /// 5. Subscribes to message events from the message interface
    /// </remarks>
    public Chatbot(ILLMClient llmClient, ILogger<Chatbot> logger, string systemPrompt, IMessageConnector messageInterface,
        string agentName = "Agent", Dictionary<string, string>? contextFields = null)
        : this(llmClient, logger, new AgentConfig { SystemPrompt = systemPrompt, Name = agentName }, messageInterface, contextFields)
    {
    }

    public Chatbot(ILLMClient llmClient, ILogger<Chatbot> logger, AgentConfig config, IMessageConnector messageInterface,
        Dictionary<string, string>? contextFields = null)
    {
        _llmClient = llmClient;
        _logger = logger;
        _messageInterface = messageInterface;
        _agentName = config.Name;
        _contextFilePath = $"{_agentName}.context.json";
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        var contextLogger = new LoggerFactory().CreateLogger<AgentContext>();

        // Try to load existing context
        _context = LoadContext() ?? new AgentContext(config.SystemPrompt, contextLogger, contextFields);

        // Apply constructor parameters to loaded context
        if (_context.SystemPrompt != config.SystemPrompt)
        {
            _context = new AgentContext(config.SystemPrompt, contextLogger, _context.ContextFields.ToDictionary(kvp => kvp.Key, kvp => kvp.Value));
        }

        if (contextFields != null)
        {
            foreach (var field in contextFields)
            {
                _context.SetContextField(field.Key, field.Value);
            }
        }

        _messageInterface.OnMessageReceived += HandleMessageReceived;

        // Set up periodic context persistence (every 5 minutes)
        _contextPersistenceTimer = new Timer(PersistContext, null, TimeSpan.Zero, TimeSpan.FromMinutes(5));

        // Set up periodic message timer with initial random interval
        _periodicMessageTimer = new Timer(_ => OnPeriodicMessageTimerCallback(), null, GetRandomInterval(), Timeout.InfiniteTimeSpan);

        // Send online announcement after ensuring Discord is ready
        _ = Task.Run(async () =>
        {
            try
            {
                // If using Discord, wait for it to be ready
                if (_messageInterface is VulcanAI.Connectors.DiscordConnector discordInterface)
                {
                    _logger.LogInformation("Waiting for Discord interface to be ready before sending online announcement...");
                    await discordInterface.ReadyTask;
                    _logger.LogInformation("Discord interface is ready, proceeding with online announcement");
                }

                var prompt = BuildFullPrompt("Generate a brief, friendly online announcement message to let users know you are available to help. Keep it concise and welcoming.");
                var response = await _llmClient.GetCompletionAsync(prompt);
                await SendMessageAsync(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending online announcement");
            }
        });
    }

    /// <summary>
    /// Loads the agent's context from a JSON file if it exists.
    /// </summary>
    /// <returns>The loaded context, or null if the file doesn't exist or cannot be loaded.</returns>
    /// <remarks>
    /// If the file exists but cannot be loaded (e.g., due to invalid JSON or file access issues),
    /// an error will be logged and null will be returned.
    /// </remarks>
    private AgentContext? LoadContext()
    {
        try
        {
            if (!File.Exists(_contextFilePath))
            {
                _logger.LogInformation("No existing context file found at {FilePath}", _contextFilePath);
                return null;
            }

            var json = File.ReadAllText(_contextFilePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                _logger.LogWarning("Context file {FilePath} is empty", _contextFilePath);
                return null;
            }

            var contextLogger = new LoggerFactory().CreateLogger<AgentContext>();
            return AgentContext.FromJson(json, contextLogger);
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "Error reading context file {FilePath}", _contextFilePath);
            return null;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Error deserializing context from {FilePath}", _contextFilePath);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error loading context from {FilePath}", _contextFilePath);
            return null;
        }
    }

    /// <summary>
    /// Persists the agent's context to a JSON file.
    /// </summary>
    /// <remarks>
    /// The context is saved to a file named {agentName}.context.json. If the save operation fails,
    /// an error will be logged but no exception will be thrown to ensure the agent continues operating.
    /// </remarks>
    private void PersistContext()
    {
        try
        {
            var json = _context.ToJson();
            File.WriteAllText(_contextFilePath, json);
            _logger.LogDebug("Persisted context to {FilePath}", _contextFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error persisting context to file {FilePath}", _contextFilePath);
        }
    }

    /// <summary>
    /// Callback for the context persistence timer.
    /// </summary>
    /// <param name="state">The state object passed to the timer callback (unused).</param>
    private void PersistContext(object? state)
    {
        PersistContext();
    }

    /// <summary>
    /// Handles incoming messages from the message interface.
    /// </summary>
    /// <param name="sender">The sender of the event.</param>
    /// <param name="e">The message event arguments containing the received message.</param>
    /// <remarks>
    /// This method is called when a message is received through the message interface.
    /// It adds the message to the conversation history, generates a response using the LLM,
    /// and sends the response back through the message interface.
    /// </remarks>
    private async void HandleMessageReceived(object? sender, VulcanAI.Connectors.Message e)
    {
        try
        {
            _logger.LogInformation("Agent received message from {Sender}: {Content}", e.Sender, e.Content);

            if (string.IsNullOrWhiteSpace(e.Content))
            {
                _logger.LogDebug("Received empty message from {Sender}, ignoring", e.Sender);
                return;
            }

            _logger.LogDebug("Adding message to conversation history");
            _context.AddMessage(e);

            _logger.LogInformation("Processing message with LLM");
            var response = await SendPromptAsync(e.Content);
            
            if (!string.IsNullOrWhiteSpace(response))
            {
                _logger.LogInformation("Sending response to message interface: {Response}", response);
                await SendMessageAsync(response);
                _logger.LogDebug("Response sent successfully");
            }
            else
            {
                _logger.LogWarning("Received empty response from LLM for message from {Sender}", e.Sender);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling message from {Sender}", e.Sender);
        }
    }

    /// <summary>
    /// Builds the full prompt including system prompt, conversation history, and context.
    /// </summary>
    /// <param name="userPrompt">The user's prompt.</param>
    /// <returns>The complete prompt to send to the LLM.</returns>
    /// <remarks>
    /// The full prompt is constructed by:
    /// 1. Adding the system prompt
    /// 2. Adding any context fields
    /// 3. Adding the conversation history (with older messages removed if needed to stay within token limits)
    /// 4. Adding the current user prompt
    /// </remarks>
    private string BuildFullPrompt(string userPrompt)
    {
        var promptBuilder = new StringBuilder();

        // Add the complete context (system prompt, context fields, and conversation history)
        promptBuilder.AppendLine(_context.ToJson());
        promptBuilder.AppendLine();

        // Add current prompt
        promptBuilder.AppendLine($"User: {userPrompt}");
        promptBuilder.AppendLine($"{_agentName}:");

        return promptBuilder.ToString();
    }

    /// <summary>
    /// Sends a prompt to the language model and returns its response.
    /// </summary>
    /// <param name="prompt">The prompt to send.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the model's response.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the prompt is null or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the prompt exceeds the maximum length or when no completion is returned.</exception>
    /// <remarks>
    /// This method sends a prompt to the language model and returns its response.
    /// The response is added to the conversation history and the context is persisted to disk.
    /// </remarks>
    public async Task<string> SendPromptAsync(string prompt)
    {
        if (string.IsNullOrEmpty(prompt))
        {
            throw new ArgumentNullException(nameof(prompt));
        }

        try
        {
            // Build the full prompt with context
            var fullPrompt = BuildFullPrompt(prompt);
            _logger.LogDebug("Sending prompt with context: {Prompt}", fullPrompt);

            var response = await _llmClient.GetCompletionAsync(fullPrompt);
            _context.AddMessage(new Message(response, _agentName));
            
            // Persist context after each message
            await PersistContextAsync();
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending prompt to language model");
            throw;
        }
    }

    /// <summary>
    /// Sends a prompt to the language model and returns its response as a strongly-typed object.
    /// </summary>
    /// <typeparam name="T">The type to deserialize the response into.</typeparam>
    /// <param name="prompt">The prompt to send.</param>
    /// <param name="options">Optional parameters for the completion request.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the deserialized response.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the prompt is null or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the prompt exceeds the maximum length or when no completion is returned.</exception>
    /// <remarks>
    /// This method sends a prompt to the language model and returns its response as a strongly-typed object.
    /// The response is added to the conversation history and the context is persisted to disk.
    /// </remarks>
    public async Task<T> SendPromptAsync<T>(string prompt, Dictionary<string, object>? options = null) where T : class
    {
        if (string.IsNullOrEmpty(prompt))
        {
            throw new ArgumentNullException(nameof(prompt));
        }

        try
        {
            // Build the full prompt with context
            var fullPrompt = BuildFullPrompt(prompt);
            _logger.LogDebug("Sending prompt with context: {Prompt}", fullPrompt);

            var response = await _llmClient.GetCompletionAsync<T>(fullPrompt, options);
            _context.AddMessage(new Message(JsonSerializer.Serialize(response), _agentName));
            
            // Persist context after each message
            await PersistContextAsync();
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending prompt to language model");
            throw;
        }
    }

    /// <summary>
    /// Persists the agent's context to disk asynchronously.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <remarks>
    /// This method saves the agent's context to a JSON file in the current directory.
    /// The file name is based on the agent's name. If an error occurs during saving,
    /// it is logged but not propagated to the caller.
    /// </remarks>
    private async Task PersistContextAsync()
    {
        try
        {
            var json = _context.ToJson();
            await File.WriteAllTextAsync(_contextFilePath, json);
            _logger.LogDebug("Context persisted to {FilePath}", _contextFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error persisting context to {FilePath}", _contextFilePath);
        }
    }

    /// <summary>
    /// Sends a message through the message interface and adds it to the conversation history.
    /// </summary>
    /// <param name="content">The content of the message to send.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the content is null or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the message interface is not configured.</exception>
    /// <remarks>
    /// The message is added to the conversation history before being sent through the message interface.
    /// This ensures that the agent's responses are included in future prompts.
    /// </remarks>
    public async Task SendMessageAsync(string content)
    {
        try
        {
            if (_messageInterface == null)
            {
                _logger.LogError("Cannot send message: Message interface is not configured");
                return;
            }

            // If the message interface is Discord, wait for it to be ready
            if (_messageInterface is VulcanAI.Connectors.DiscordConnector discordInterface)
            {
                if (!discordInterface.IsReady)
                {
                    _logger.LogInformation("Waiting for Discord interface to be ready...");
                    await discordInterface.ReadyTask;
                    _logger.LogInformation("Discord interface is now ready");
                }
            }

            _logger.LogInformation("Agent sending message: {Content}", content);
            var message = new Message(content, _agentName, "Agent");
            await _messageInterface.SendMessageAsync(message);
            _logger.LogDebug("Message sent through interface successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message through interface");
        }
    }

    /// <summary>
    /// Gets the conversation history of the agent.
    /// </summary>
    public Message[] ConversationHistory => _context.ConversationHistory;

    /// <summary>
    /// Gets the context fields of the agent.
    /// </summary>
    public IReadOnlyDictionary<string, string> Context => _context.ContextFields;

    /// <summary>
    /// Sets a value in the agent's context.
    /// </summary>
    /// <param name="key">The key to set.</param>
    /// <param name="value">The value to set.</param>
    public void SetContext(string key, object value)
    {
        _context.SetContextField(key, value.ToString() ?? string.Empty);
    }

    /// <summary>
    /// Removes a value from the agent's context.
    /// </summary>
    /// <param name="key">The key to remove.</param>
    /// <returns>True if the key was found and removed, false otherwise.</returns>
    public bool RemoveContext(string key)
    {
        return _context.RemoveContextField(key);
    }

    /// <summary>
    /// Serializes the agent's context to a JSON string.
    /// </summary>
    /// <returns>A JSON string representing the agent's context.</returns>
    /// <remarks>
    /// The serialized context includes the system prompt, conversation history, and context fields.
    /// This is the same format used for persisting the context to disk.
    /// </remarks>
    public string ToJson()
    {
        return _context.ToJson();
    }

    /// <summary>
    /// Creates a new agent instance from a serialized context.
    /// </summary>
    /// <param name="llmClient">The language model client to use.</param>
    /// <param name="logger">The logger instance to use.</param>
    /// <param name="json">The JSON string containing the serialized context.</param>
    /// <param name="messageInterface">Optional message interface to use.</param>
    /// <param name="agentName">Optional name for the agent.</param>
    /// <returns>A new instance of the agent with the deserialized context.</returns>
    public IAgent FromJson(
        ILLMClient llmClient,
        ILogger<IAgent> logger,
        string json,
        IMessageConnector? messageInterface = null,
        string agentName = "Agent")
    {
        var contextLogger = new LoggerFactory().CreateLogger<AgentContext>();
        var context = AgentContext.FromJson(json, contextLogger);
        var contextFields = context.ContextFields.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        
        // Create a null message interface if none provided
        var interfaceToUse = messageInterface ?? new NullMessageInterface();
        
        // Cast the logger to the correct type
        var chatbotLogger = logger as ILogger<Chatbot> ?? 
            throw new InvalidOperationException("Logger must be convertible to ILogger<Chatbot>");
        
        return new Chatbot(llmClient, chatbotLogger, context.SystemPrompt, interfaceToUse, agentName, contextFields);
    }

    /// <summary>
    /// Disposes of the agent's resources.
    /// </summary>
    /// <remarks>
    /// This method:
    /// 1. Stops the context persistence timer
    /// 2. Performs a final save of the context to disk
    /// 3. Releases any other unmanaged resources
    /// </remarks>
    public void Dispose()
    {
        _contextPersistenceTimer?.Dispose();
        _periodicMessageTimer?.Dispose();
        PersistContext();
    }
} 
