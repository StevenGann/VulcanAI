using System;
using System.Threading.Tasks;

namespace VulcanAI.Core.Connectors
{
    /// <summary>
    /// A null implementation of IMessageInterface that does nothing.
    /// This is used when no message interface is provided, to avoid null references.
    /// </summary>
    public class NullMessageInterface : IMessageConnector
    {
#pragma warning disable CS0067 // The event is never used - this is expected for a null implementation
        public event EventHandler<Message>? OnMessageReceived;
#pragma warning restore CS0067

        public Task StartAsync()
        {
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            return Task.CompletedTask;
        }

        public Task SendMessageAsync(Message message)
        {
            return Task.CompletedTask;
        }
    }
} 