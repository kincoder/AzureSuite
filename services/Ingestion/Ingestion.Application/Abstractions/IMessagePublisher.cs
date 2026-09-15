namespace AzureSuite.Ingestion.Application.Abstractions
{
    /// <summary>
    /// Publishing contract for accepted messages, defined here (Application layer) and
    /// implemented in Infrastructure — command handlers never depend on the Service Bus SDK
    /// directly, matching Catalog's IMessageTypeRepository split.
    /// </summary>
    public interface IMessagePublisher
    {
        /// <summary>Publishes an accepted message. Does not return until the broker has
        /// durably accepted it — callers rely on this to know whether it's safe to
        /// acknowledge the original request.</summary>
        Task PublishAsync(Guid messageId, string messageType, string version, string payload, CancellationToken cancellationToken);
    }
}
