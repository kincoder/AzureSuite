using System.Diagnostics.CodeAnalysis;
using Azure.Messaging.ServiceBus;
using AzureSuite.Processing.Application.Messages.Commands.RecordInboundMessage;

namespace AzureSuite.Processing.Functions.Functions
{
    /// <summary>Turns a raw broker message into a <see cref="RecordInboundMessageCommand"/>,
    /// checking the application properties Ingestion stamps on every message
    /// (<c>messageType</c>, <c>version</c>; see Ingestion's ServiceBusMessagePublisher). Kept
    /// separate from the trigger so the "is this message well-formed" decision is unit-testable
    /// without the Functions host.</summary>
    public static class InboundMessageReader
    {
        public static bool TryRead(
            ServiceBusReceivedMessage message,
            DateTime receivedAtUtc,
            [NotNullWhen(true)] out RecordInboundMessageCommand? command,
            [NotNullWhen(false)] out string? problem)
        {
            command = null;

            var messageType = ReadProperty(message, "messageType");
            var version = ReadProperty(message, "version");

            var missing = new List<string>();
            if (string.IsNullOrWhiteSpace(messageType))
            {
                missing.Add("messageType");
            }
            if (string.IsNullOrWhiteSpace(version))
            {
                missing.Add("version");
            }

            if (missing.Count > 0)
            {
                problem = $"Missing required application properties: {string.Join(", ", missing)}.";
                return false;
            }

            problem = null;
            command = new RecordInboundMessageCommand(
                message.MessageId,
                messageType!,
                version!,
                message.Body.ToMemory().Length,
                receivedAtUtc);
            return true;
        }

        private static string? ReadProperty(ServiceBusReceivedMessage message, string name)
        {
            return message.ApplicationProperties.TryGetValue(name, out var value) ? value?.ToString() : null;
        }
    }
}
