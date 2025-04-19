using System;
using System.Threading.Tasks;
using VulcanAI.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace VulcanAI.Core.Interfaces;

/// <summary>
/// Implements the <see cref="IMessageInterface"/> for console-based communication.
/// This interface allows the agent to send and receive messages through the system console.
/// Messages are displayed in the format [Sender] Content, and user input is captured when Enter is pressed.
/// </summary>
/// <remarks>
/// The ConsoleInterface provides a simple way to interact with the agent through the command line.
/// It runs a background task to continuously read user input and raises events when messages are received.
/// All operations are asynchronous and include proper logging for debugging and monitoring.
/// </remarks>
public class ConsoleInterface : IMessageInterface
{
    private readonly ILogger<ConsoleInterface> _logger;
    private bool _isRunning;
    private Task? _inputTask;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsoleInterface"/> class.
    /// </summary>
    /// <param name="logger">The logger instance for recording diagnostic information.</param>
    /// <exception cref="ArgumentNullException">Thrown when the logger parameter is null.</exception>
    public ConsoleInterface(ILogger<ConsoleInterface> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Occurs when a message is received from the console input.
    /// </summary>
    /// <remarks>
    /// This event is raised whenever the user types a message and presses Enter.
    /// The message sender is set to "User" and the channel is set to "Console".
    /// </remarks>
    public event EventHandler<Message>? OnMessageReceived;

    /// <summary>
    /// Starts the console interface and begins listening for user input.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// This method starts a background task that continuously reads input from the console.
    /// If the interface is already running, this method returns immediately.
    /// </remarks>
    public async Task StartAsync()
    {
        if (_isRunning)
        {
            return;
        }

        _isRunning = true;
        _inputTask = Task.Run(ReadConsoleInput);
        _logger.LogInformation("Console interface started");
    }

    /// <summary>
    /// Stops the console interface and stops listening for user input.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// This method stops the background input reading task and waits for it to complete.
    /// If the interface is not running, this method returns immediately.
    /// </remarks>
    public async Task StopAsync()
    {
        if (!_isRunning)
        {
            return;
        }

        _isRunning = false;
        if (_inputTask != null)
        {
            await _inputTask;
        }
        _logger.LogInformation("Console interface stopped");
    }

    /// <summary>
    /// Sends a message to the console output.
    /// </summary>
    /// <param name="message">The message to send.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// Messages are displayed in the format [Sender] Content.
    /// This method is thread-safe and can be called from any thread.
    /// </remarks>
    public async Task SendMessageAsync(Message message)
    {
        Console.WriteLine($"[{message.Sender}] {message.Content}");
        _logger.LogDebug("Sent message: {Content}", message.Content);
    }

    /// <summary>
    /// Continuously reads input from the console while the interface is running.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// This method runs in a background task and continuously reads input from the console.
    /// When input is received, it creates a new Message and raises the OnMessageReceived event.
    /// The method stops when the interface is stopped or when the application is terminated.
    /// </remarks>
    private async Task ReadConsoleInput()
    {
        while (_isRunning)
        {
            var input = await Task.Run(() => Console.ReadLine());
            if (input == null)
            {
                continue;
            }

            _logger.LogDebug("Received message from console: {Content}", input);
            OnMessageReceived?.Invoke(this, new Message(input, "User", "Console"));
        }
    }
} 