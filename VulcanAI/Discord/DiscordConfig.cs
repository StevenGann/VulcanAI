namespace VulcanAI.Connectors
{
    /// <summary>
    /// Represents the configuration settings for the Discord interface.
    /// </summary>
    public class DiscordConfig
    {
        /// <summary>
        /// Gets or sets the Discord bot token.
        /// </summary>
        public string Token { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the ID of the Discord channel to monitor.
        /// </summary>
        public ulong ChannelId { get; set; }
    }
} 