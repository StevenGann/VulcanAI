using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VulcanAI.LLM;
using Microsoft.Extensions.Logging;
using VulcanAI.Connectors;

namespace VulcanAI.Agent;

/// <summary>
/// Defines the interface for an AI agent that can process messages and generate responses.
/// </summary>
public interface IAgent
{
    /// <summary>
    /// Gets the conversation history of the agent.
    /// </summary>
    Message[] ConversationHistory { get; }

    /// <summary>
    /// Gets the context fields of the agent.
    /// </summary>
    IReadOnlyDictionary<string, string> Context { get; }

    /// <summary>
    /// Sends a prompt to the language model and returns its response.
    /// </summary>
    /// <param name="prompt">The prompt to send.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the model's response.</returns>
    Task<string> SendPromptAsync(string prompt);

    /// <summary>
    /// Sends a prompt to the language model and returns its response as a strongly-typed object.
    /// </summary>
    /// <typeparam name="T">The type to deserialize the response into.</typeparam>
    /// <param name="prompt">The prompt to send.</param>
    /// <param name="options">Optional parameters for the completion request.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the deserialized response.</returns>
    Task<T> SendPromptAsync<T>(string prompt, Dictionary<string, object>? options = null) where T : class;

    /// <summary>
    /// Sends a message through the message interface and adds it to the conversation history.
    /// </summary>
    /// <param name="content">The content of the message to send.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task SendMessageAsync(string content);

    /// <summary>
    /// Sets a value in the agent's context.
    /// </summary>
    /// <param name="key">The key to set.</param>
    /// <param name="value">The value to set.</param>
    void SetContext(string key, object value);

    /// <summary>
    /// Removes a value from the agent's context.
    /// </summary>
    /// <param name="key">The key to remove.</param>
    /// <returns>True if the key was found and removed, false otherwise.</returns>
    bool RemoveContext(string key);

    /// <summary>
    /// Serializes the agent's context to a JSON string.
    /// </summary>
    /// <returns>A JSON string representing the agent's context.</returns>
    string ToJson();

    /// <summary>
    /// Creates a new agent instance from a serialized context.
    /// </summary>
    /// <param name="llmClient">The language model client to use.</param>
    /// <param name="logger">The logger instance to use.</param>
    /// <param name="json">The JSON string containing the serialized context.</param>
    /// <param name="messageInterface">Optional message interface to use.</param>
    /// <param name="agentName">Optional name for the agent.</param>
    /// <returns>A new instance of the agent with the deserialized context.</returns>
    IAgent FromJson(
        ILLMClient llmClient,
        ILogger<IAgent> logger,
        string json,
        IMessageConnector? messageInterface = null,
        string agentName = "Agent");
}