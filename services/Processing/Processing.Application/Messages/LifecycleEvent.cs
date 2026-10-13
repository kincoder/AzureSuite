namespace AzureSuite.Processing.Application.Messages
{
    /// <summary>One entry in a message's lifecycle log, written to Cosmos DB.</summary>
    /// <param name="MessageId">The broker message id. A string rather than a Guid: messages that
    /// reach the dead-letter queue may not have come from Ingestion (e.g. sent by hand from the
    /// portal), so their id isn't guaranteed to be a UUID.</param>
    /// <param name="Status">One of the <see cref="LifecycleStatus"/> values.</param>
    /// <param name="MessageType">Declared message type, when the message carried one.</param>
    /// <param name="Version">Declared schema version, when the message carried one.</param>
    /// <param name="Reason">Why the message is in this state, e.g. the dead-letter reason. Null
    /// for <see cref="LifecycleStatus.Received"/>.</param>
    /// <param name="PayloadSizeBytes">Size of the message body. The payload itself is never
    /// copied into the log.</param>
    /// <param name="OccurredAtUtc">When Processing observed this state.</param>
    public record LifecycleEvent(
        string MessageId,
        string Status,
        string? MessageType,
        string? Version,
        string? Reason,
        int PayloadSizeBytes,
        DateTime OccurredAtUtc);
}
