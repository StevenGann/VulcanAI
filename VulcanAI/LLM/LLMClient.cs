using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace VulcanAI.LLM;

public abstract class LLMClient : ILLMClient
{
    protected readonly HttpClient _httpClient;
    protected readonly ILogger? _logger;
    protected readonly JsonSerializerOptions _jsonOptions;

    public int MaxPromptLength { get; set; } = 4096;

    protected LLMClient(HttpClient httpClient, ILogger? logger = null)
    {
        _httpClient = httpClient;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public abstract Task<string> GetCompletionAsync(string prompt);

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