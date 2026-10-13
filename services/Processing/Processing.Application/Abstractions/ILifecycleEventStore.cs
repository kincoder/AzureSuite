using AzureSuite.Processing.Application.Messages;

namespace AzureSuite.Processing.Application.Abstractions
{
    /// <summary>Persistence for the message lifecycle log. Defined here, implemented in
    /// Infrastructure against Cosmos DB, so handlers never touch the Cosmos SDK directly.</summary>
    public interface ILifecycleEventStore
    {
        /// <summary>Writes an event. Idempotent per (MessageId, Status): recording the same
        /// pair twice (e.g. after a Service Bus redelivery) leaves one entry, not two.</summary>
        Task RecordAsync(LifecycleEvent lifecycleEvent, CancellationToken cancellationToken);

        /// <summary>Deletes every event last written before <paramref name="cutoffUtc"/>.</summary>
        /// <returns>How many events were deleted.</returns>
        Task<int> PurgeOlderThanAsync(DateTime cutoffUtc, CancellationToken cancellationToken);
    }
}
