using VulcanAI.Core.Interfaces;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using System.Threading.Tasks;

namespace VulcanAI.Core.Agent;


/// <summary>
/// Represents the context of an agent, including its system prompt, conversation history, and context fields.
/// The context manages token limits by removing older messages when needed to stay within the maximum token count.
/// </summary>
public class AgentContext
{
    private readonly List<Message> _conversationHistory;
    private readonly Dictionary<string, string> _contextFields;
    private readonly ILogger<AgentContext> _logger;

    /// <summary>
    /// Gets or sets the maximum number of tokens allowed in the context.
    /// </summary>
    /// <remarks>
    /// This value is used to limit the size of the context when serializing to JSON.
    /// When the context exceeds this limit, older messages are removed from the conversation history
    /// until the context is within the limit. The default value is 4096 tokens.
    /// </remarks>
    public int MaxTokens { get; set; } = 4096;

    /// <summary>
    /// Gets the system prompt that defines the agent's behavior and personality.
    /// </summary>
    /// <remarks>
    /// The system prompt is always included in the context and is not affected by the token limit.
    /// It should be concise but descriptive enough to guide the agent's behavior.
    /// </remarks>
    public string SystemPrompt { get; }

    /// <summary>
    /// Gets the conversation history as a read-only array of messages.
    /// </summary>
    /// <remarks>
    /// The conversation history includes all messages sent and received by the agent.
    /// When serializing to JSON, older messages may be removed to stay within the token limit.
    /// </remarks>
    public Message[] ConversationHistory => _conversationHistory.ToArray();

    /// <summary>
    /// Gets the context fields as a read-only dictionary.
    /// </summary>
    /// <remarks>
    /// Context fields are used to store additional information that can be referenced
    /// in prompts and responses. They are always included in the context and are not
    /// affected by the token limit.
    /// </remarks>
    public IReadOnlyDictionary<string, string> ContextFields => _contextFields;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentContext"/> class.
    /// </summary>
    /// <param name="systemPrompt">The system prompt that defines the agent's behavior and personality.</param>
    /// <param name="logger">The logger instance for recording diagnostic information.</param>
    /// <param name="initialContextFields">Optional initial context fields for the agent.</param>
    /// <remarks>
    /// The system prompt is required and cannot be changed after creation.
    /// Context fields can be added, modified, or removed at any time.
    /// </remarks>
    public AgentContext(string systemPrompt, ILogger<AgentContext> logger, Dictionary<string, string>? initialContextFields = null)
    {
        SystemPrompt = systemPrompt ?? throw new ArgumentNullException(nameof(systemPrompt));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _conversationHistory = new List<Message>();
        _contextFields = initialContextFields ?? new Dictionary<string, string>();
    }

    /// <summary>
    /// Adds a message to the conversation history.
    /// </summary>
    /// <param name="message">The message to add.</param>
    /// <remarks>
    /// Messages are added to the end of the conversation history.
    /// When serializing to JSON, older messages may be removed to stay within the token limit.
    /// </remarks>
    public void AddMessage(Message message)
    {
        _conversationHistory.Add(message);
    }

    /// <summary>
    /// Sets a context field value.
    /// </summary>
    /// <param name="key">The key of the context field.</param>
    /// <param name="value">The value to set.</param>
    /// <remarks>
    /// If the key already exists, its value will be updated.
    /// Context fields are always included in the context and are not affected by the token limit.
    /// </remarks>
    public void SetContextField(string key, string value)
    {
        _contextFields[key] = value;
    }

    /// <summary>
    /// Gets a context field value.
    /// </summary>
    /// <param name="key">The key of the context field.</param>
    /// <returns>The value of the context field, or null if not found.</returns>
    /// <remarks>
    /// Context fields are used to store additional information that can be referenced
    /// in prompts and responses. They are always included in the context and are not
    /// affected by the token limit.
    /// </remarks>
    public string? GetContextField(string key)
    {
        return _contextFields.TryGetValue(key, out var value) ? value : null;
    }

    /// <summary>
    /// Removes a context field.
    /// </summary>
    /// <param name="key">The key of the context field to remove.</param>
    /// <returns>True if the field was removed, false if it didn't exist.</returns>
    /// <remarks>
    /// Context fields are used to store additional information that can be referenced
    /// in prompts and responses. They are always included in the context and are not
    /// affected by the token limit.
    /// </remarks>
    public bool RemoveContextField(string key)
    {
        return _contextFields.Remove(key);
    }

    /// <summary>
    /// Serializes the context to a JSON string, respecting the maximum token limit.
    /// </summary>
    /// <returns>A JSON string representing the context, with older messages removed if necessary to stay within token limits.</returns>
    /// <remarks>
    /// The serialized context includes:
    /// 1. The system prompt
    /// 2. Any context fields
    /// 3. The conversation history (with older messages removed if needed to stay within token limits)
    /// The token limit is enforced by removing older messages from the conversation history
    /// until the context is within the limit. The system prompt and context fields are always included.
    /// </remarks>
    public string ToJson()
    {
        // Create a copy of the context with potentially truncated history
        var contextToSerialize = new AgentContextData
        {
            SystemPrompt = SystemPrompt,
            ContextFields = new Dictionary<string, string>(_contextFields),
            ConversationHistory = new List<Message>()
        };

        // Remove duplicate messages and add to serialization list
        var seenMessages = new HashSet<(string Sender, string Content)>();
        foreach (var message in _conversationHistory)
        {
            var key = (message.Sender, message.Content);
            if (!seenMessages.Contains(key))
            {
                seenMessages.Add(key);
                contextToSerialize.ConversationHistory.Add(message);
            }
            else
            {
                _logger.LogDebug("Removing duplicate message from {Sender} with content: {Content}", message.Sender, message.Content);
            }
        }

        // Remove older messages if needed to stay within token limit
        while (GetTokenCount(contextToSerialize) > MaxTokens && contextToSerialize.ConversationHistory.Count > 0)
        {
            _logger.LogDebug("Removing oldest message from context to stay within token limit");
            contextToSerialize.ConversationHistory.RemoveAt(0);
        }

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        return JsonSerializer.Serialize(contextToSerialize, options);
    }

    /// <summary>
    /// Creates a new context from a JSON string.
    /// </summary>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <param name="logger">The logger instance for recording diagnostic information.</param>
    /// <returns>A new instance of <see cref="AgentContext"/> with the deserialized data.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the JSON cannot be deserialized into an AgentContext.</exception>
    /// <remarks>
    /// The JSON string should be in the format produced by the <see cref="ToJson"/> method.
    /// The deserialized context will have the same system prompt, context fields, and conversation history
    /// as the original context, subject to the token limit.
    /// </remarks>
    public static AgentContext FromJson(string json, ILogger<AgentContext> logger)
    {
        if (string.IsNullOrEmpty(json))
        {
            throw new ArgumentException("JSON string cannot be null or empty", nameof(json));
        }

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        try
        {
            var contextData = JsonSerializer.Deserialize<AgentContextData>(json, options);
            if (contextData == null)
            {
                throw new InvalidOperationException("Failed to deserialize AgentContext from JSON");
            }

            // Validate required fields
            if (string.IsNullOrWhiteSpace(contextData.SystemPrompt))
            {
                throw new InvalidOperationException("SystemPrompt cannot be null or empty");
            }

            if (contextData.ContextFields == null)
            {
                throw new InvalidOperationException("ContextFields cannot be null");
            }

            if (contextData.ConversationHistory == null)
            {
                throw new InvalidOperationException("ConversationHistory cannot be null");
            }

            // Create context with validated data
            var context = new AgentContext(contextData.SystemPrompt, logger, contextData.ContextFields);
            
            // Add messages with validation
            foreach (var message in contextData.ConversationHistory)
            {
                if (message == null)
                {
                    logger.LogWarning("Skipping null message in conversation history");
                    continue;
                }
                
                if (string.IsNullOrWhiteSpace(message.Content))
                {
                    logger.LogWarning("Skipping message with empty content in conversation history");
                    continue;
                }
                
                context.AddMessage(message);
            }

            return context;
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Error deserializing AgentContext from JSON");
            throw new InvalidOperationException("Invalid JSON format for AgentContext", ex);
        }
    }

    /// <summary>
    /// Calculates the token count for a given text string.
    /// </summary>
    /// <param name="text">The text to calculate tokens for.</param>
    /// <returns>The number of tokens in the text.</returns>
    /// <remarks>
    /// This method uses a more accurate token estimation based on the actual tokenization used by language models:
    /// - Words are split into subwords using common patterns
    /// - Punctuation is generally part of the same token as the word
    /// - Numbers are generally part of the same token as the word
    /// - Special characters and whitespace are counted separately
    /// </remarks>
    public int GetTokenCount(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        // Split text into words and count tokens
        var words = text.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        var tokenCount = 0;

        foreach (var word in words)
        {
            // Count tokens for each word based on common patterns
            if (word.Length <= 4)
            {
                // Short words are usually one token
                tokenCount++;
            }
            else
            {
                // Longer words are split into subwords
                // Common patterns: camelCase, snake_case, kebab-case, etc.
                var subwords = word.Split(new[] { '_', '-', '.', ',', ';', ':', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var subword in subwords)
                {
                    if (subword.Length <= 4)
                    {
                        tokenCount++;
                    }
                    else
                    {
                        // Estimate tokens for longer subwords
                        tokenCount += (int)Math.Ceiling(subword.Length / 4.0);
                    }
                }
            }
        }

        // Add tokens for special characters and whitespace
        tokenCount += text.Count(c => char.IsWhiteSpace(c) || char.IsPunctuation(c));

        return tokenCount;
    }

    /// <summary>
    /// Calculates the token count for a given context data object.
    /// </summary>
    /// <param name="contextData">The context data to calculate tokens for.</param>
    /// <returns>The total number of tokens in the context data.</returns>
    /// <remarks>
    /// This method calculates tokens for:
    /// 1. The system prompt
    /// 2. All context fields (key + value)
    /// 3. All messages in the conversation history
    /// 4. JSON structure overhead (estimated based on actual JSON structure)
    /// </remarks>
    public int GetTokenCount(AgentContextData contextData)
    {
        var tokenCount = 0;

        // Count system prompt tokens
        tokenCount += GetTokenCount(contextData.SystemPrompt);

        // Count context field tokens
        foreach (var field in contextData.ContextFields)
        {
            tokenCount += GetTokenCount(field.Key);
            tokenCount += GetTokenCount(field.Value);
            // JSON structure overhead for key-value pair: "key": "value"
            tokenCount += 4; // quotes, colon, space
        }

        // Count message tokens
        foreach (var message in contextData.ConversationHistory)
        {
            tokenCount += GetTokenCount(message.Content);
            tokenCount += GetTokenCount(message.Sender);
            // JSON structure overhead for message: {"content": "...", "sender": "..."}
            tokenCount += 8; // quotes, colons, commas, braces
        }

        // Add JSON structure overhead for the entire context
        tokenCount += 4; // outer braces and newlines

        _logger.LogDebug("Token count for context data: {TokenCount}", tokenCount);
        return tokenCount;
    }
} 