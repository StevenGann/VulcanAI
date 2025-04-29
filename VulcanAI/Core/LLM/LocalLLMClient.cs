using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using VulcanAI.Connectors;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using System.Linq;

namespace VulcanAI.LLM;

/// <summary>
/// Implements the <see cref="ILLMClient"/> interface for local language model services,
/// supporting both OpenAI-compatible and LM Studio formats. The client handles prompt
/// validation, token limits, and response deserialization.
/// </summary>
public class LocalLLMClient : ILLMClient
{
    private readonly IHttpClient _httpClient;
    private readonly string _model;
    private readonly string _baseUrl;
    private readonly bool _useOpenAIFormat;
    private readonly ILogger<LocalLLMClient> _logger;

    /// <summary>
    /// Gets or sets the maximum length of prompts that can be sent to the LLM.
    /// </summary>
    /// <remarks>
    /// This value is used to validate prompts before sending them to the LLM.
    /// The default value is 4096 tokens, which is a common limit for many language models.
    /// </remarks>
    public int MaxPromptLength { get; set; } = 4096;

    private class OpenAIRequest
    {
        public string Model { get; set; } = string.Empty;
        public Message[] Messages { get; set; } = Array.Empty<Message>();
    }

    private class LMStudioRequest
    {
        public string Model { get; set; } = string.Empty;
        public string Prompt { get; set; } = string.Empty;
        public Message[] Messages { get; set; } = Array.Empty<Message>();
    }

    private class Message
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;
        
        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }

    private class OpenAIResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;
        
        [JsonPropertyName("object")]
        public string Object { get; set; } = string.Empty;
        
        [JsonPropertyName("created")]
        public long Created { get; set; }
        
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;
        
        [JsonPropertyName("choices")]
        public Choice[]? Choices { get; set; }
        
        [JsonPropertyName("usage")]
        public Usage? Usage { get; set; }
        
        [JsonPropertyName("stats")]
        public Dictionary<string, object>? Stats { get; set; }
        
        [JsonPropertyName("system_fingerprint")]
        public string? SystemFingerprint { get; set; }
    }

    private class Choice
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }
        
        [JsonPropertyName("logprobs")]
        public object? Logprobs { get; set; }
        
        [JsonPropertyName("finish_reason")]
        public string? FinishReason { get; set; }
        
        [JsonPropertyName("message")]
        public Message? Message { get; set; }
    }

    private class Usage
    {
        [JsonPropertyName("prompt_tokens")]
        public int PromptTokens { get; set; }
        
        [JsonPropertyName("completion_tokens")]
        public int CompletionTokens { get; set; }
        
        [JsonPropertyName("total_tokens")]
        public int TotalTokens { get; set; }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalLLMClient"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client used to communicate with the LLM service.</param>
    /// <param name="model">The name of the language model to use.</param>
    /// <param name="baseUrl">The base URL of the LLM service.</param>
    /// <param name="logger">The logger instance for recording diagnostic information.</param>
    /// <param name="useOpenAIFormat">Whether to use the OpenAI API format for requests. If false, uses LM Studio format.</param>
    /// <remarks>
    /// The client supports two request formats:
    /// 1. OpenAI format: Uses the standard OpenAI API request structure
    /// 2. LM Studio format: Uses a simplified request structure compatible with LM Studio
    /// Both formats use the same endpoint (/v1/chat/completions) but with different request bodies.
    /// </remarks>
    public LocalLLMClient(
        IHttpClient httpClient,
        string model,
        string baseUrl,
        ILogger<LocalLLMClient> logger,
        bool useOpenAIFormat = true)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _baseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _useOpenAIFormat = useOpenAIFormat;
    }

    /// <summary>
    /// Validates that the prompt is within the maximum length allowed by the LLM client.
    /// </summary>
    /// <param name="prompt">The prompt to validate.</param>
    /// <exception cref="InvalidOperationException">Thrown when the prompt exceeds the maximum length.</exception>
    /// <remarks>
    /// This method checks if the prompt exceeds the maximum length allowed by the LLM client.
    /// If it does, an exception is thrown. The maximum length is determined by the MaxTokens property.
    /// </remarks>
    private void ValidatePrompt(string prompt)
    {
        if (string.IsNullOrEmpty(prompt))
        {
            throw new ArgumentNullException(nameof(prompt));
        }

        // Estimate tokens using GPT tokenizer patterns
        int tokenCount = 0;

        // Split into words and special characters
        var parts = prompt.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var part in parts)
        {
            // Handle special characters and punctuation
            var segments = new List<string>();
            var currentSegment = new StringBuilder();
            
            foreach (var c in part)
            {
                if (char.IsPunctuation(c) || char.IsSymbol(c))
                {
                    if (currentSegment.Length > 0)
                    {
                        segments.Add(currentSegment.ToString());
                        currentSegment.Clear();
                    }
                    segments.Add(c.ToString());
                }
                else
                {
                    currentSegment.Append(c);
                }
            }
            
            if (currentSegment.Length > 0)
            {
                segments.Add(currentSegment.ToString());
            }

            // Process each segment
            foreach (var segment in segments)
            {
                if (segment.Length == 1)
                {
                    // Single characters (including punctuation) are usually 1 token
                    tokenCount++;
                }
                else
                {
                    // For words, estimate based on common subword patterns
                    var word = segment.ToLowerInvariant();
                    
                    // Common prefixes (1 token each)
                    if (word.StartsWith("un") || word.StartsWith("re") || 
                        word.StartsWith("in") || word.StartsWith("dis"))
                    {
                        tokenCount++;
                        word = word.Substring(2);
                    }
                    
                    // Common suffixes (1 token each)
                    if (word.EndsWith("ing") || word.EndsWith("ed") || 
                        word.EndsWith("ly") || word.EndsWith("tion"))
                    {
                        tokenCount++;
                        word = word.Substring(0, word.Length - (word.EndsWith("tion") ? 4 : 2));
                    }
                    
                    // Remaining word parts (roughly 1 token per 4 chars)
                    tokenCount += Math.Max(1, (word.Length + 3) / 4);
                }
            }
        }

        // Add tokens for whitespace (newlines count as 1, spaces as fraction)
        tokenCount += prompt.Count(c => c == '\n');
        tokenCount += prompt.Count(c => c == ' ') / 4;

        // Add overhead for JSON structure
        tokenCount += 4; // Basic message structure

        _logger.LogDebug("Estimated token count for prompt: {TokenCount}", tokenCount);

        if (tokenCount > MaxPromptLength)
        {
            throw new InvalidOperationException($"Prompt exceeds maximum length of {MaxPromptLength} tokens. Estimated length: {tokenCount} tokens.");
        }
    }

    /// <summary>
    /// Gets a completion from the language model.
    /// </summary>
    /// <param name="prompt">The prompt to send to the language model.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the model's response.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the prompt is null or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the prompt exceeds the maximum length or when no completion is returned.</exception>
    /// <remarks>
    /// This method sends a prompt to the language model and returns its response.
    /// The response format depends on whether the client is configured to use the OpenAI-compatible format
    /// or the LM Studio format. In either case, the method extracts the completion text from the response
    /// and returns it as a string.
    /// </remarks>
    public async Task<string> GetCompletionAsync(string prompt)
    {
        ValidatePrompt(prompt);

        try
        {
            object requestBody = _useOpenAIFormat
                ? new OpenAIRequest
                {
                    Model = _model,
                    Messages = new[]
                    {
                        new Message { Role = "user", Content = prompt }
                    }
                }
                : CreateLMStudioRequest(prompt);

            var content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            _logger.LogDebug("Sending request to LLM API: {Request}", JsonSerializer.Serialize(requestBody));

            var response = await _httpClient.PostAsync($"{_baseUrl}/v1/chat/completions", content);
            
            try
            {
                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException ex)
            {
                var errorResponse = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to get response from LLM API. Status: {Status}, Response: {Response}", 
                    response.StatusCode, errorResponse);
                throw new InvalidOperationException($"Failed to get response from LLM API: {ex.Message}", ex);
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("Received response from LLM API: {Response}", responseContent);
            
            var responseObject = JsonSerializer.Deserialize<OpenAIResponse>(responseContent);

            if (responseObject?.Choices == null || responseObject.Choices.Length == 0)
            {
                throw new InvalidOperationException("No choices returned from LLM API");
            }

            var message = responseObject.Choices[0].Message;
            if (message == null)
            {
                throw new InvalidOperationException("Message is null in LLM API response");
            }

            return message.Content ?? string.Empty;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Invalid JSON response from LLM API", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating response from LLM");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<T> GetCompletionAsync<T>(string prompt, Dictionary<string, object>? options = null) where T : class
    {
        ValidatePrompt(prompt);

        try
        {
            object requestBody = _useOpenAIFormat
                ? new OpenAIRequest
                {
                    Model = _model,
                    Messages = new[]
                    {
                        new Message { Role = "user", Content = prompt }
                    }
                }
                : new LMStudioRequest
                {
                    Model = _model,
                    Prompt = prompt
                };

            var requestContent = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync($"{_baseUrl}/v1/chat/completions", requestContent);
            
            try
            {
                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException ex)
            {
                throw new InvalidOperationException($"Failed to get response from LLM API: {ex.Message}", ex);
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var responseObject = JsonSerializer.Deserialize<OpenAIResponse>(responseContent);

            if (responseObject?.Choices == null || responseObject.Choices.Length == 0)
            {
                throw new InvalidOperationException("No choices returned from LLM API");
            }

            var message = responseObject.Choices[0].Message;
            if (message == null)
            {
                throw new InvalidOperationException("Message is null in LLM API response");
            }

            var messageContent = message.Content;
            if (string.IsNullOrEmpty(messageContent))
            {
                throw new InvalidOperationException("Content is null or empty in LLM API response");
            }

            try
            {
                return JsonSerializer.Deserialize<T>(messageContent) 
                    ?? throw new InvalidOperationException("Deserialization resulted in null object");
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException("Failed to deserialize LLM response content", ex);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating response from LLM");
            throw;
        }
    }

    /// <summary>
    /// Creates a request body in the OpenAI API format.
    /// </summary>
    /// <param name="prompt">The prompt to include in the request.</param>
    /// <returns>A dictionary containing the request parameters.</returns>
    /// <remarks>
    /// The OpenAI-compatible format uses a messages array with a single user message
    /// containing the prompt. The model and temperature parameters are also included.
    /// </remarks>
    private Dictionary<string, object> CreateOpenAIRequest(string prompt)
    {
        return new Dictionary<string, object>
        {
            { "model", _model },
            { "messages", new[]
                {
                    new { role = "user", content = prompt }
                }
            },
            { "temperature", 0.7 }
        };
    }

    /// <summary>
    /// Creates a request body in the LM Studio format.
    /// </summary>
    /// <param name="prompt">The prompt to include in the request.</param>
    /// <returns>A dictionary containing the request parameters.</returns>
    /// <remarks>
    /// The LM Studio format uses a single prompt string and includes additional parameters
    /// for controlling the generation process, such as max_tokens and stop sequences.
    /// </remarks>
    private Dictionary<string, object> CreateLMStudioRequest(string prompt)
    {
        return new Dictionary<string, object>
        {
            { "model", _model },
            { "messages", new[]
                {
                    new { role = "user", content = prompt }
                }
            },
            { "max_tokens", 2000 },
            { "temperature", 0.7 },
            { "stop", new[] { "User:", "Assistant:" } }
        };
    }
} 