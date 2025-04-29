using System;
using System.Threading.Tasks;

namespace VulcanAI.Connectors
{
    /// <summary>
    /// Represents a message in the chat system with metadata about the sender and timing.
    /// </summary>
    public record Message
    {
        /// <summary>
        /// Gets the content of the message.
        /// </summary>
        public string Content { get; init; }

        /// <summary>
        /// Gets the name of the message sender.
        /// </summary>
        public string Sender { get; init; }

        /// <summary>
        /// Gets the timestamp when the message was created.
        /// </summary>
        public DateTime Timestamp { get; init; }

        /// <summary>
        /// Gets the optional channel name where the message was sent.
        /// </summary>
        public string? Channel { get; init; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Message"/> class.
        /// </summary>
        /// <param name="content">The content of the message.</param>
        /// <param name="sender">The name of the message sender.</param>
        /// <param name="channel">The optional channel name where the message was sent.</param>
        public Message(string content, string sender, string? channel = null)
        {
            Content = content;
            Sender = sender;
            Timestamp = DateTime.UtcNow;
            Channel = channel;
        }
    }

    /// <summary>
    /// Defines the interface for message-based communication systems.
    /// </summary>
    public interface IMessageConnector
    {
        /// <summary>
        /// Occurs when a message is received from the interface.
        /// </summary>
        event EventHandler<Message> OnMessageReceived;

        /// <summary>
        /// Sends a message through the interface.
        /// </summary>
        /// <param name="message">The message to send.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SendMessageAsync(Message message);

        /// <summary>
        /// Starts the message interface.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task StartAsync();

        /// <summary>
        /// Stops the message interface.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task StopAsync();
    }
} 