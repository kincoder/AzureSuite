using Azure.Messaging.ServiceBus;

namespace Processing.Infrastructure.Tests.TestDoubles
{
    /// <summary>Hands out a single <see cref="FakeServiceBusReceiver"/>. The Service Bus SDK
    /// types expose protected constructors and virtual members specifically so they can be
    /// subclassed like this in tests.</summary>
    public class FakeServiceBusClient : ServiceBusClient
    {
        public FakeServiceBusClient(FakeServiceBusReceiver receiver)
        {
            Receiver = receiver;
        }

        public FakeServiceBusReceiver Receiver { get; }

        public string? RequestedQueueName { get; private set; }

        public ServiceBusReceiverOptions? RequestedOptions { get; private set; }

        public override ServiceBusReceiver CreateReceiver(string queueName, ServiceBusReceiverOptions options)
        {
            RequestedQueueName = queueName;
            RequestedOptions = options;
            return Receiver;
        }
    }

    /// <summary>Serves queued messages in batches and records how each one was settled.</summary>
    public class FakeServiceBusReceiver : ServiceBusReceiver
    {
        private readonly Queue<ServiceBusReceivedMessage> _pending;

        public FakeServiceBusReceiver(IEnumerable<ServiceBusReceivedMessage> messages)
        {
            _pending = new Queue<ServiceBusReceivedMessage>(messages);
        }

        public List<string> Completed { get; } = new();

        public List<string> Abandoned { get; } = new();

        public List<int> RequestedBatchSizes { get; } = new();

        public bool Disposed { get; private set; }

        public override Task<IReadOnlyList<ServiceBusReceivedMessage>> ReceiveMessagesAsync(
            int maxMessages, TimeSpan? maxWaitTime = null, CancellationToken cancellationToken = default)
        {
            RequestedBatchSizes.Add(maxMessages);

            var batch = new List<ServiceBusReceivedMessage>();
            while (batch.Count < maxMessages && _pending.TryDequeue(out var message))
            {
                batch.Add(message);
            }

            return Task.FromResult<IReadOnlyList<ServiceBusReceivedMessage>>(batch);
        }

        public override Task CompleteMessageAsync(ServiceBusReceivedMessage message, CancellationToken cancellationToken = default)
        {
            Completed.Add(message.MessageId);
            return Task.CompletedTask;
        }

        public override Task AbandonMessageAsync(
            ServiceBusReceivedMessage message,
            IDictionary<string, object>? propertiesToModify = null,
            CancellationToken cancellationToken = default)
        {
            Abandoned.Add(message.MessageId);
            return Task.CompletedTask;
        }

        public override ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
