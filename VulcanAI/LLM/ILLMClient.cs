using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace VulcanAI.LLM;

/// <summary>
/// Interface for LLM clients
/// </summary>
public interface ILLMClient
{
    /// <summary>
    /// Maximum length of the prompt that can be sent to the LLM
    /// </summary>
    int MaxPromptLength { get; set; }

    /// <summary>
    /// Gets a completion from the LLM
    /// </summary>
    /// <param name="prompt">The prompt to send to the LLM</param>
    /// <returns>The completion from the LLM</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when the prompt is null or empty</exception>
    /// <exception cref="System.InvalidOperationException">Thrown when the prompt length exceeds MaxPromptLength</exception>
    Task<string> GetCompletionAsync(string prompt);

    /// <summary>
    /// Gets a completion from the LLM and deserializes it into the specified type
    /// </summary>
    /// <typeparam name="T">The type to deserialize the completion into</typeparam>
    /// <param name="prompt">The prompt to send to the LLM</param>
    /// <param name="options">Additional options for the completion</param>
    /// <returns>The deserialized completion</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when the prompt is null or empty</exception>
    /// <exception cref="System.InvalidOperationException">Thrown when the prompt length exceeds MaxPromptLength or deserialization fails</exception>
    Task<T> GetCompletionAsync<T>(string prompt, Dictionary<string, object>? options = null) where T : class;
} 