using System;
using System.IO;
using System.Threading.Tasks;
using ObsidianDB;
using VulcanAI.Connectors;
using VulcanAI.Core.Knowledge;

namespace VulcanAI.Core.Knowledge
{
    /// <summary>
    /// Implementation of IKnowledgeStore that uses ObsidianDB as the underlying storage.
    /// </summary>
    /// <remarks>
    /// This class provides a bridge between the VulcanAI knowledge system and Obsidian vaults.
    /// It allows querying and managing knowledge stored in Obsidian markdown files.
    /// </remarks>
    public class ObsidianKnowledge : IKnowledgeConnector
    {
        private readonly ObsidianDB.ObsidianDB _db;
        private bool _isStarted;

        /// <summary>
        /// Initializes a new instance of the ObsidianKnowledge class.
        /// </summary>
        /// <param name="vaultPath">The path to the Obsidian vault directory.</param>
        /// <exception cref="ArgumentNullException">Thrown when vaultPath is null or empty.</exception>
        /// <exception cref="DirectoryNotFoundException">Thrown when the vault directory does not exist.</exception>
        public ObsidianKnowledge(string vaultPath)
        {
            if (string.IsNullOrWhiteSpace(vaultPath))
                throw new ArgumentNullException(nameof(vaultPath));

            if (!Directory.Exists(vaultPath))
                throw new DirectoryNotFoundException($"Vault path does not exist: {vaultPath}");

            _db = new ObsidianDB.ObsidianDB(vaultPath);
        }

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
        /// <exception cref="InvalidOperationException">Thrown when the store is not started.</exception>
        public Task<KnowledgeCollection> QueryKnowledgeAsync(string query, int? maxResults = null)
        {
            if (!_isStarted)
                throw new InvalidOperationException("Knowledge store must be started before querying.");

            if (string.IsNullOrWhiteSpace(query))
                throw new ArgumentException("Query cannot be empty or whitespace.", nameof(query));

            var collection = new KnowledgeCollection();

            // For now, we'll use a simple text-based search
            // TODO: Implement more sophisticated search using ObsidianDB's capabilities
            foreach (var note in _db.GetNotes())
            {
                // Calculate a simple relevance score based on text matching
                double score = CalculateRelevanceScore(note, query);
                if (score > 0)
                {
                    var knowledge = new Knowledge(note.Body, note.Path);
                    collection.Add(knowledge, score);
                }
            }

            // Apply maxResults if specified
            if (maxResults.HasValue)
            {
                return Task.FromResult(collection.GetTopItems(maxResults.Value));
            }

            return Task.FromResult(collection);
        }

        /// <summary>
        /// Adds a new knowledge item to the store.
        /// </summary>
        /// <param name="knowledge">The knowledge item to add.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when knowledge is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the store is not started.</exception>
        /// <exception cref="NotImplementedException">Thrown as this feature is not yet implemented in ObsidianDB.</exception>
        public Task AddKnowledgeAsync(Knowledge knowledge)
        {
            if (!_isStarted)
                throw new InvalidOperationException("Knowledge store must be started before adding knowledge.");

            if (knowledge == null)
                throw new ArgumentNullException(nameof(knowledge));

            // TODO: Implement this when ObsidianDB supports note creation
            throw new NotImplementedException("Adding knowledge to Obsidian vault is not yet implemented.");
        }

        /// <summary>
        /// Starts the knowledge store and performs any necessary initialization.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the store is already started.</exception>
        public Task StartAsync()
        {
            if (_isStarted)
                throw new InvalidOperationException("Knowledge store is already started.");

            _db.ScanNotes();
            _isStarted = true;

            return Task.CompletedTask;
        }

        /// <summary>
        /// Stops the knowledge store and performs any necessary cleanup.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the store is not started.</exception>
        public Task StopAsync()
        {
            if (!_isStarted)
                throw new InvalidOperationException("Knowledge store is not started.");

            _db.Dispose();
            _isStarted = false;

            return Task.CompletedTask;
        }

        /// <summary>
        /// Calculates a relevance score for a note based on the query.
        /// </summary>
        /// <param name="note">The note to score.</param>
        /// <param name="query">The search query.</param>
        /// <returns>A relevance score between 0 and 1.</returns>
        private double CalculateRelevanceScore(Note note, string query)
        {
            // Simple text-based scoring
            // TODO: Implement more sophisticated scoring using ObsidianDB's capabilities
            string content = note.Body.ToLowerInvariant();
            string searchQuery = query.ToLowerInvariant();

            if (content.Contains(searchQuery))
            {
                // Basic scoring based on occurrence count
                int occurrences = content.Split(searchQuery).Length - 1;
                return Math.Min(1.0, occurrences * 0.1);
            }

            return 0;
        }
    }
} 