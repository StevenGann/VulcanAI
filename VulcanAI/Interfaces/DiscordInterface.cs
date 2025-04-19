using System;
using System.Threading.Tasks;
using VulcanAI.Core.Interfaces;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace VulcanAI.Core.Interfaces;

public class DiscordInterface : IMessageInterface
{
    private readonly DiscordSocketClient _client;
    private readonly ILogger<DiscordInterface> _logger;
    private readonly string _token;
    private readonly ulong _channelId;
    private bool _isRunning;

    public DiscordInterface(
        DiscordSocketClient client,
        ILogger<DiscordInterface> logger,
        string token,
        ulong channelId)
    {
        _client = client;
        _logger = logger;
        _token = token;
        _channelId = channelId;
    }

    public async Task StartAsync()
    {
        if (_isRunning)
        {
            return;
        }

        _client.MessageReceived += HandleMessageReceived;
        await _client.LoginAsync(TokenType.Bot, _token);
        await _client.StartAsync();
        _isRunning = true;
        _logger.LogInformation("Discord bot started");
    }

    public async Task StopAsync()
    {
        if (!_isRunning)
        {
            return;
        }

        _client.MessageReceived -= HandleMessageReceived;
        await _client.StopAsync();
        _isRunning = false;
        _logger.LogInformation("Discord bot stopped");
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

    public event EventHandler<Message>? OnMessageReceived;

    public async Task SendMessageAsync(Message message)
    {
        var channel = await _client.GetChannelAsync(_channelId) as IMessageChannel;
        if (channel == null)
        {
            _logger.LogError("Failed to get channel {ChannelId}", _channelId);
            return;
        }

        await channel.SendMessageAsync(message.Content);
        _logger.LogDebug("Sent message: {Content}", message.Content);
    }
} 