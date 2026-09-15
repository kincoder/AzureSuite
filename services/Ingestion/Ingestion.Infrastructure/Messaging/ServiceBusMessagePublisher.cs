using AzureSuite.Ingestion.Application.Abstractions;
using Azure.Messaging.ServiceBus;

namespace AzureSuite.Ingestion.Infrastructure.Messaging
{
    /// <summary>Publishes accepted messages to the raw Service Bus queue. Stamps the minted
    /// MessageId onto the broker message's own native MessageId property — this is also the
    /// property Service Bus's built-in duplicate detection keys on, should that be enabled
    /// later (Standard tier only; not enabled this increment, see the Ingestion design spec).</summary>
    public class ServiceBusMessagePublisher : IMessagePublisher, IAsyncDisposable
    {
        private readonly ServiceBusSender _sender;

        public ServiceBusMessagePublisher(ServiceBusClient client, string queueName)
        {
            _sender = client.CreateSender(queueName);
        }

        public async Task PublishAsync(Guid messageId, string messageType, string version, string payload, CancellationToken cancellationToken)
        {
            var message = new ServiceBusMessage(payload)
            {
                MessageId = messageId.ToString()
            };
            message.ApplicationProperties["messageType"] = messageType;
            message.ApplicationProperties["version"] = version;

            await _sender.SendMessageAsync(message, cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            await _sender.DisposeAsync();
        }
    }
}
