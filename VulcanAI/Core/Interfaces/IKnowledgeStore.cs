using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using VulcanAI.Core.Knowledge;

namespace VulcanAI.Core.Interfaces
{
    /// <summary>
    /// Defines the interface for a knowledge store that can store and retrieve knowledge items.
    /// </summary>
    /// <remarks>
    /// This interface provides the core functionality for managing knowledge in the system,
    /// including querying, adding, and lifecycle management of the knowledge store.
    /// Implementations should be thread-safe and handle concurrent access appropriately.
    /// </remarks>
    public interface IKnowledgeStore
    {
        /// <summary>
        /// Queries the knowledge store for relevant knowledge items based on the provided query.
        /// </summary>
        /// <param name="query">The search query string.</param>
        /// <param name="maxResults">The maximum number of results to return. If null, returns all results.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains
        /// a <see cref="KnowledgeCollection"/> of relevant knowledge items sorted by relevance.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when query is null.</exception>
        /// <exception cref="ArgumentException">Thrown when query is empty or whitespace.</exception>
        Task<KnowledgeCollection> QueryKnowledgeAsync(string query, int? maxResults = null);

        /// <summary>
        /// Adds a new knowledge item to the store.
        /// </summary>
        /// <param name="knowledge">The knowledge item to add.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when knowledge is null.</exception>
        Task AddKnowledgeAsync(Knowledge.Knowledge knowledge);

        /// <summary>
        /// Starts the knowledge store and performs any necessary initialization.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the store is already started.</exception>
        Task StartAsync();

        /// <summary>
        /// Stops the knowledge store and performs any necessary cleanup.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the store is not started.</exception>
        Task StopAsync();
    }
}