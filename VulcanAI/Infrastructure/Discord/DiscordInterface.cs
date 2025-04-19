using System;
using System.Threading.Tasks;
using VulcanAI.Core.Interfaces;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace VulcanAI.Infrastructure.Discord
{
    /// <summary>
    /// Implements the <see cref="IMessageInterface"/> for Discord, allowing the agent to send and receive messages
    /// through a Discord channel. Messages longer than Discord's 2000-character limit are automatically split
    /// into multiple messages.
    /// </summary>
    public class DiscordInterface : IMessageInterface
    {
        private readonly DiscordSocketClient _client;
        private readonly ILogger<DiscordInterface> _logger;
        private readonly string _token;
        private readonly ulong _channelId;
        private bool _isRunning;
        private TaskCompletionSource<bool> _readyTaskSource;

        /// <summary>
        /// Gets a task that completes when the Discord client is ready.
        /// </summary>
        public Task<bool> ReadyTask => _readyTaskSource.Task;

        /// <summary>
        /// Gets whether the Discord client is ready to send and receive messages.
        /// </summary>
        public bool IsReady => _client.ConnectionState == ConnectionState.Connected;

        /// <summary>
        /// Initializes a new instance of the <see cref="DiscordInterface"/> class.
        /// </summary>
        /// <param name="client">The Discord socket client.</param>
        /// <param name="logger">The logger instance.</param>
        /// <param name="token">The Discord bot token.</param>
        /// <param name="channelId">The ID of the channel to monitor and send messages to.</param>
        public DiscordInterface(
            DiscordSocketClient client,
            ILogger<DiscordInterface> logger,
            string token,
            ulong channelId)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _token = token ?? throw new ArgumentNullException(nameof(token));
            _channelId = channelId;
            _readyTaskSource = new TaskCompletionSource<bool>();

            _client.Log += LogAsync;
            _client.Ready += ReadyAsync;
            _client.MessageReceived += HandleMessageReceived;
        }

        /// <summary>
        /// Occurs when a message is received from Discord.
        /// </summary>
        public event EventHandler<Message>? OnMessageReceived;

        /// <summary>
        /// Starts the Discord client and begins listening for messages.
        /// </summary>
        public async Task StartAsync()
        {
            if (_isRunning)
            {
                _logger.LogWarning("Discord interface is already running");
                return;
            }

            try
            {
                _logger.LogInformation("Starting Discord interface...");
                await _client.LoginAsync(TokenType.Bot, _token);
                await _client.StartAsync();
                _isRunning = true;
                _logger.LogInformation("Discord interface started successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start Discord interface");
                throw;
            }
        }

        /// <summary>
        /// Stops the Discord client and stops listening for messages.
        /// </summary>
        public async Task StopAsync()
        {
            if (!_isRunning)
            {
                _logger.LogWarning("Discord interface is not running");
                return;
            }

            try
            {
                _logger.LogInformation("Stopping Discord interface...");
                _client.MessageReceived -= HandleMessageReceived;
                await _client.StopAsync();
                _isRunning = false;
                _logger.LogInformation("Discord interface stopped successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop Discord interface");
                throw;
            }
        }

        /// <summary>
        /// Sends a message to the configured Discord channel.
        /// </summary>
        /// <param name="message">The message to send.</param>
        public async Task SendMessageAsync(Message message)
        {
            if (!_isRunning)
            {
                _logger.LogWarning("Cannot send message: Discord interface is not running");
                return;
            }

            try
            {
                var channel = _client.GetChannel(_channelId) as IMessageChannel;
                if (channel == null)
                {
                    _logger.LogError("Failed to get Discord channel with ID {ChannelId}", _channelId);
                    return;
                }

                // Split message if it exceeds Discord's 2000 character limit
                const int maxLength = 2000;
                if (message.Content.Length > maxLength)
                {
                    _logger.LogInformation("Message exceeds Discord's character limit, splitting into multiple messages");
                    for (int i = 0; i < message.Content.Length; i += maxLength)
                    {
                        int length = Math.Min(maxLength, message.Content.Length - i);
                        string chunk = message.Content.Substring(i, length);
                        await channel.SendMessageAsync(chunk);
                    }
                }
                else
                {
                    await channel.SendMessageAsync(message.Content);
                }

                _logger.LogDebug("Message sent to Discord channel {ChannelId}", _channelId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send message to Discord channel {ChannelId}", _channelId);
                throw;
            }
        }

        private Task LogAsync(LogMessage log)
        {
            var logLevel = log.Severity switch
            {
                LogSeverity.Critical => LogLevel.Critical,
                LogSeverity.Error => LogLevel.Error,
                LogSeverity.Warning => LogLevel.Warning,
                LogSeverity.Info => LogLevel.Information,
                LogSeverity.Verbose => LogLevel.Debug,
                LogSeverity.Debug => LogLevel.Trace,
                _ => LogLevel.Information
            };

            _logger.Log(logLevel, log.Exception, "Discord.NET: {Message}", log.Message);
            return Task.CompletedTask;
        }

        private Task ReadyAsync()
        {
            _logger.LogInformation("Discord client is ready");
            _readyTaskSource.TrySetResult(true);
            return Task.CompletedTask;
        }

        private Task HandleMessageReceived(SocketMessage message)
        {
            if (message.Author.IsBot || message.Channel.Id != _channelId)
            {
                return Task.CompletedTask;
            }

            _logger.LogDebug("Received message from {User}: {Content}", message.Author.Username, message.Content);
            OnMessageReceived?.Invoke(this, new Message(message.Content, message.Author.Username, message.Channel.Name));
            return Task.CompletedTask;
        }
    }
}