using AzureSuite.Domain.Enums;
using AzureSuite.Domain.ValueObjects;

namespace AzureSuite.Domain.Entities;

/// <summary>
/// A simplified ISO 20022 pacs.008 (FIToFICustomerCreditTransfer) message: one
/// customer credit transfer instruction between a debtor and a creditor.
/// </summary>
public class Pacs008Message
{
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>ISO 20022 GroupHeader/MsgId - unique identifier assigned by the sender.</summary>
    public required string MessageId { get; set; }

    /// <summary>End-to-end identifier that follows the transaction across all parties.</summary>
    public required string EndToEndId { get; set; }

    public DateTimeOffset CreationDateTime { get; set; }

    public required decimal Amount { get; set; }

    /// <summary>ISO 4217 currency code, e.g. "EUR".</summary>
    public required string Currency { get; set; }

    public required PartyAccount Debtor { get; set; }

    public required PartyAccount Creditor { get; set; }

    public string? RemittanceInformation { get; set; }

    public MessageStatus Status { get; set; } = MessageStatus.Received;

    public DateTimeOffset ReceivedAt { get; init; } = DateTimeOffset.UtcNow;
}
