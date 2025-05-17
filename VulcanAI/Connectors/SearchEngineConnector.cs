using System;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using SerpApi;
using VulcanAI.Knowledge;

namespace VulcanAI.Connectors
{
    /// <summary>
    /// Implements IKnowledgeConnector using Google Search via SerpApi.
    /// </summary>
    public class SearchEngineConnector : IKnowledgeConnector
    {
        private readonly string _apiKey;
        private bool _isStarted;
        private GoogleSearch _searchClient;

        /// <summary>
        /// Initializes a new instance of the SearchEngineConnector.
        /// </summary>
        /// <param name="apiKey">The SerpApi API key.</param>
        /// <exception cref="ArgumentNullException">Thrown when apiKey is null.</exception>
        /// <exception cref="ArgumentException">Thrown when apiKey is empty or whitespace.</exception>
        public SearchEngineConnector(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("API key cannot be empty or whitespace.", nameof(apiKey));

            _apiKey = apiKey;
            _isStarted = false;
        }

        /// <inheritdoc/>
        public async Task<KnowledgeCollection> QueryKnowledgeAsync(string query, int? maxResults = null)
        {
            if (!_isStarted)
                throw new InvalidOperationException("SearchEngineConnector is not started.");

            if (string.IsNullOrWhiteSpace(query))
                throw new ArgumentException("Query cannot be empty or whitespace.", nameof(query));

            var parameters = new Hashtable
            {
                { "q", query },
                { "hl", "en" },
                { "google_domain", "google.com" }
            };

            try
            {
                var results = new KnowledgeCollection();
                var searchData = await Task.Run(() => _searchClient.GetJson());

                // Process organic search results
                if (searchData["organic_results"] is JArray organicResults)
                {
                    foreach (JObject result in organicResults)
                    {
                        string title = result["title"]?.ToString() ?? "";
                        string link = result["link"]?.ToString() ?? "";
                        string snippet = result["snippet"]?.ToString() ?? "";

                        // Combine title and snippet for content
                        string content = $"{title}\n{snippet}".Trim();
                        
                        if (!string.IsNullOrWhiteSpace(content) && !string.IsNullOrWhiteSpace(link))
                        {
                            var knowledge = new VulcanAI.Knowledge.Knowledge(content, link);
                            // Use position as inverse score (first results are more relevant)
                            double score = 1.0 / (results.Count + 1);
                            results.Add(knowledge, score);

                            if (maxResults.HasValue && results.Count >= maxResults.Value)
                                break;
                        }
                    }
                }

                return results;
            }
            catch (SerpApiSearchException ex)
            {
                throw new InvalidOperationException("Failed to perform search query.", ex);
            }
        }

        /// <inheritdoc/>
        public Task AddKnowledgeAsync(VulcanAI.Knowledge.Knowledge knowledge)
        {
            throw new NotSupportedException("SearchEngineConnector does not support adding knowledge.");
        }

        /// <inheritdoc/>
        public Task StartAsync()
        {
            if (_isStarted)
                throw new InvalidOperationException("SearchEngineConnector is already started.");

            _searchClient = new GoogleSearch(new Hashtable(), _apiKey);
            _isStarted = true;
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task StopAsync()
        {
            if (!_isStarted)
                throw new InvalidOperationException("SearchEngineConnector is not started.");

            _searchClient?.Close();
            _searchClient = null;
            _isStarted = false;
            return Task.CompletedTask;
        }
    }
} 