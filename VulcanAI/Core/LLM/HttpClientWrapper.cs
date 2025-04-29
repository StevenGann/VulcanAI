using System;
using System.Net.Http;
using System.Threading.Tasks;
using VulcanAI.Core.Connectors;

namespace VulcanAI.Core.LLM;

/// <summary>
/// Wrapper class that implements IHttpClient by delegating to a real HttpClient instance.
/// </summary>
public class HttpClientWrapper : IHttpClient
{
    private readonly HttpClient _httpClient;

    public HttpClientWrapper(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<HttpResponseMessage> PostAsync(string requestUri, HttpContent content)
    {
        return await _httpClient.PostAsync(requestUri, content);
    }
} 