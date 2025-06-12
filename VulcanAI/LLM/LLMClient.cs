using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace VulcanAI.LLM;

/// <summary>
/// Base abstract class for LLM clients that handle communication with language model services.
/// </summary>
/// <remarks>
/// This class provides common functionality for sending requests to language model endpoints,
/// handling JSON serialization/deserialization, and logging. It is designed to be extended
/// by specific implementations that handle different LLM service formats.
/// </remarks>
public abstract class LLMClient : ILLMClient
{
    /// <summary>
    /// The HTTP client used for sending requests to the LLM service.
    /// </summary>
    protected readonly HttpClient _httpClient;

    /// <summary>
    /// Optional logger for recording diagnostic information.
    /// </summary>
    protected readonly ILogger? _logger;

    /// <summary>
    /// JSON serialization options used for request and response handling.
    /// </summary>
    protected readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Gets or sets the maximum length of prompts that can be sent to the LLM.
    /// </summary>
    /// <remarks>
    /// This value is used to validate prompts before sending them to the LLM.
    /// The default value is 4096 tokens, which is a common limit for many language models.
    /// </remarks>
    public int MaxPromptLength { get; set; } = 4096;

    /// <summary>
    /// Initializes a new instance of the <see cref="LLMClient"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client to use for requests.</param>
    /// <param name="logger">Optional logger for recording diagnostic information.</param>
    protected LLMClient(HttpClient httpClient, ILogger? logger = null)
    {
        _httpClient = httpClient;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    /// <summary>
    /// Sends a prompt to the language model and returns its response as a string.
    /// </summary>
    /// <param name="prompt">The prompt to send.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the model's response.</returns>
    public abstract Task<string> GetCompletionAsync(string prompt);

    /// <summary>
    /// Sends a prompt to the language model and returns its response as a strongly-typed object.
    /// </summary>
    /// <typeparam name="T">The type to deserialize the response into.</typeparam>
    /// <param name="prompt">The prompt to send.</param>
    /// <param name="options">Optional parameters for the completion request.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the deserialized response.</returns>
    /// <exception cref="JsonException">Thrown when the response cannot be deserialized into the specified type.</exception>
    public async Task<T> GetCompletionAsync<T>(string prompt, Dictionary<string, object>? options = null) where T : class
    {
        var response = await GetCompletionAsync(prompt);
        try
        {
            return JsonSerializer.Deserialize<T>(response, _jsonOptions) 
                ?? throw new JsonException("Failed to deserialize response");
        }
        catch (JsonException ex)
        {
            _logger?.LogError(ex, "Failed to deserialize response: {Response}", response);
            throw;
        }
    }

    /// <summary>
    /// Sends a request to the specified endpoint with the given request body.
    /// </summary>
    /// <param name="endpoint">The endpoint to send the request to.</param>
    /// <param name="requestBody">The request body to send.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the response content.</returns>
    /// <exception cref="HttpRequestException">Thrown when the request fails.</exception>
    protected async Task<string> SendRequestAsync(string endpoint, object requestBody)
    {
        var json = JsonSerializer.Serialize(requestBody, _jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        _logger?.LogDebug("Sending request to {Endpoint}: {Json}", endpoint, json);

        var response = await _httpClient.PostAsync(endpoint, content);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync();
        _logger?.LogDebug("Received response: {Response}", responseContent);

        return responseContent;
    }
} 