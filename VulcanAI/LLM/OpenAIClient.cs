using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using VulcanAI.Connectors;

namespace VulcanAI.LLM;

public class OpenAIClient : ILLMClient
{
    private readonly string _apiKey;
    private readonly string _model;
    private readonly IHttpClient _httpClient;
    private readonly ILogger<OpenAIClient> _logger;
    private const string BaseUrl = "https://api.openai.com/v1/chat/completions";
    private readonly JsonSerializerOptions _jsonOptions;

    public int MaxPromptLength { get; set; } = 4096;

    public OpenAIClient(string apiKey, string model, IHttpClient httpClient, ILogger<OpenAIClient> logger)
    {
        _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<string> GetCompletionAsync(string prompt)
    {
        if (string.IsNullOrEmpty(prompt))
        {
            throw new ArgumentNullException(nameof(prompt));
        }

        if (prompt.Length > MaxPromptLength)
        {
            throw new InvalidOperationException($"Prompt length exceeds maximum allowed length of {MaxPromptLength}");
        }

        try
        {
            var requestBody = new
            {
                model = _model,
                messages = new[]
                {
                    new { role = "user", content = prompt }
                }
            };

            var content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, BaseUrl);
            request.Headers.Add("Authorization", $"Bearer {_apiKey}");
            request.Content = content;

            var response = await _httpClient.PostAsync(request.RequestUri.ToString(), content);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            var responseObject = JsonSerializer.Deserialize<OpenAIResponse>(responseContent);

            if (responseObject?.Choices == null || responseObject.Choices.Length == 0)
            {
                throw new InvalidOperationException("No choices returned from OpenAI API");
            }

            var message = responseObject.Choices[0].Message;
            if (message?.Content == null)
            {
                throw new InvalidOperationException("Message content is null in OpenAI API response");
            }

            return message.Content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating response from OpenAI");
            throw;
        }
    }

    public async Task<T> GetCompletionAsync<T>(string prompt, Dictionary<string, object>? options = null) where T : class
    {
        var response = await GetCompletionAsync(prompt);
        try
        {
            return JsonSerializer.Deserialize<T>(response, _jsonOptions) 
                ?? throw new InvalidOperationException("Deserialization resulted in null object");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize response: {Response}", response);
            throw new InvalidOperationException("Failed to deserialize the response", ex);
        }
    }

    private class OpenAIResponse
    {
        public Choice[]? Choices { get; set; }
    }

    private class Choice
    {
        public Message? Message { get; set; }
    }

    private class Message
    {
        public string? Content { get; set; }
    }
} 