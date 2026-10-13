using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;

namespace Processing.Functions.Tests.TestDoubles
{
    /// <summary>Records how the trigger settled each message instead of talking to the host.</summary>
    public class FakeServiceBusMessageActions : ServiceBusMessageActions
    {
        public List<string> Completed { get; } = new();

        public List<(string MessageId, string? Reason, string? Description)> DeadLettered { get; } = new();

        public override Task CompleteMessageAsync(ServiceBusReceivedMessage message, CancellationToken cancellationToken = default)
        {
            Completed.Add(message.MessageId);
            return Task.CompletedTask;
        }

        public override Task DeadLetterMessageAsync(
            ServiceBusReceivedMessage message,
            Dictionary<string, object>? propertiesToModify = null,
            string? deadLetterReason = null,
            string? deadLetterErrorDescription = null,
            CancellationToken cancellationToken = default)
        {
            DeadLettered.Add((message.MessageId, deadLetterReason, deadLetterErrorDescription));
            return Task.CompletedTask;
        }
    }
}
