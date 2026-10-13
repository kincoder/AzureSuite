using Azure.Messaging.ServiceBus;
using AzureSuite.Processing.Application.Abstractions;
using AzureSuite.Processing.Application.Messages;

namespace AzureSuite.Processing.Infrastructure.Messaging
{
    /// <summary>Drains the dead-letter sub-queue of the inbound queue. A dead-letter queue is not
    /// a separate entity you create: every Service Bus queue has one, addressed by opening a
    /// receiver with <see cref="SubQueue.DeadLetter"/>.</summary>
    public class ServiceBusDeadLetterQueue : IDeadLetterQueue
    {
        private const int BatchSize = 50;
        private static readonly TimeSpan ReceiveWait = TimeSpan.FromSeconds(5);

        private readonly ServiceBusClient _client;
        private readonly string _queueName;

        public ServiceBusDeadLetterQueue(ServiceBusClient client, string queueName)
        {
            _client = client;
            _queueName = queueName;
        }

        public static DeadLetteredMessage ToDeadLetteredMessage(ServiceBusReceivedMessage message)
        {
            return new DeadLetteredMessage(
                message.MessageId,
                ReadProperty(message, "messageType"),
                ReadProperty(message, "version"),
                message.DeadLetterReason,
                message.DeadLetterErrorDescription,
                message.Body.ToMemory().Length);
        }

        public async Task<int> DrainAsync(
            Func<DeadLetteredMessage, CancellationToken, Task> handler,
            int maxMessages,
            CancellationToken cancellationToken)
        {
            await using var receiver = _client.CreateReceiver(
                _queueName, new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });

            var drained = 0;
            while (drained < maxMessages)
            {
                var batch = await receiver.ReceiveMessagesAsync(
                    Math.Min(BatchSize, maxMessages - drained), ReceiveWait, cancellationToken);
                if (batch.Count == 0)
                {
                    break;
                }

                foreach (var message in batch)
                {
                    try
                    {
                        await handler(ToDeadLetteredMessage(message), cancellationToken);
                    }
                    catch
                    {
                        // Release it now rather than waiting for the lock to expire, so the next
                        // run sees it straight away. CancellationToken.None: if the failure was a
                        // cancellation, the abandon should still go through.
                        await receiver.AbandonMessageAsync(message, cancellationToken: CancellationToken.None);
                        throw;
                    }

                    await receiver.CompleteMessageAsync(message, cancellationToken);
                    drained++;
                }
            }

            return drained;
        }

        private static string? ReadProperty(ServiceBusReceivedMessage message, string name)
        {
            return message.ApplicationProperties.TryGetValue(name, out var value) ? value?.ToString() : null;
        }
    }
}
