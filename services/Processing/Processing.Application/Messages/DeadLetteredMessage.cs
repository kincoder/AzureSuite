namespace AzureSuite.Processing.Application.Messages
{
    /// <summary>What housekeeping needs to know about a message sitting in the dead-letter
    /// queue: which message it was and why Service Bus (or Processing) gave up on it.</summary>
    /// <param name="DeadLetterReason">Set by whoever dead-lettered it, e.g.
    /// "MaxDeliveryCountExceeded" from Service Bus itself or "MalformedMessage" from
    /// Processing's trigger.</param>
    /// <param name="DeadLetterErrorDescription">Free-text detail accompanying the reason.</param>
    public record DeadLetteredMessage(
        string MessageId,
        string? MessageType,
        string? Version,
        string? DeadLetterReason,
        string? DeadLetterErrorDescription,
        int PayloadSizeBytes);
}
