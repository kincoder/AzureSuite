namespace AzureSuite.Ingestion.Application.Messages
{
    /// <summary>
    /// Returned to the caller once a message has been accepted and durably published.
    /// </summary>
    /// <param name="MessageId">The identifier minted for this message — a UUIDv7, sortable
    /// by acceptance time.</param>
    /// <param name="MessageType">The message type as submitted, e.g. "pacs.008".</param>
    /// <param name="Version">The schema version as submitted, e.g. "1.0".</param>
    public record IngestedMessageDto(Guid MessageId, string MessageType, string Version);
}
