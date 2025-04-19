using VulcanAI.Core.Interfaces;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VulcanAI.Core
{
    /// <summary>
    /// Represents the data structure used for serializing and deserializing the agent context.
    /// </summary>
    public class AgentContextData
    {
        /// <summary>
        /// Gets or sets the system prompt.
        /// </summary>
        [JsonRequired]
        public string SystemPrompt { get; set; } = null!;

        /// <summary>
        /// Gets or sets the context fields.
        /// </summary>
        [JsonRequired]
        public Dictionary<string, string> ContextFields { get; set; } = null!;

        /// <summary>
        /// Gets or sets the conversation history.
        /// </summary>
        [JsonRequired]
        public List<Message> ConversationHistory { get; set; } = null!;
    }
} 