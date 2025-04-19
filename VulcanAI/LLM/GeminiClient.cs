using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Logging;

namespace VulcanAI.Core.LLM;

public class GeminiClient : LLMClient
{
    private readonly string _apiKey;
    private readonly string _model;
    private readonly string _baseUrl;

    public GeminiClient(
        HttpClient httpClient,
        string apiKey,
        string model = "gemini-pro",
        string baseUrl = "https://generativelanguage.googleapis.com/v1beta",
        ILogger? logger = null)
        : base(httpClient, logger)
    {
        _apiKey = apiKey;
        _model = model;
        _baseUrl = baseUrl.TrimEnd('/');
    }

    public override async Task<string> GetCompletionAsync(string prompt)
    {
        var requestBody = new
        {
            model = _model,
            prompt = prompt,
            temperature = 0.7,
            max_tokens = 1000
        };

        return await SendRequestAsync($"https://generativelanguage.googleapis.com/v1/models/{_model}:generateContent?key={_apiKey}", requestBody);
    }

    private class GeminiResponse
    {
        public GeminiCandidate[] Candidates { get; set; } = Array.Empty<GeminiCandidate>();

        public class GeminiCandidate
        {
            public GeminiContent Content { get; set; } = new();

            public class GeminiContent
            {
                public GeminiPart[] Parts { get; set; } = Array.Empty<GeminiPart>();

                public class GeminiPart
                {
                    public string Text { get; set; } = string.Empty;
                }
            }
        }
    }
} 