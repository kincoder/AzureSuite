using System.ComponentModel.DataAnnotations;

namespace AzureSuite.Application.Messages;

/// <summary>
/// Input shape for submitting a new PACS.008 message. Field length limits mirror the
/// ISO 20022 pacs.008 schema (e.g. MessageId/EndToEndId max 35 chars) so invalid input
/// is rejected with a clean 400 here, instead of surfacing as a raw SQL truncation error.
/// </summary>
public record CreatePacs008MessageRequest(
    [property: Required, StringLength(35)] string MessageId,
    [property: Required, StringLength(35)] string EndToEndId,
    [property: Range(0.01, double.MaxValue)] decimal Amount,
    [property: Required, StringLength(3, MinimumLength = 3)] string Currency,
    [property: Required, StringLength(140)] string DebtorName,
    [property: Required, StringLength(34)] string DebtorIban,
    [property: Required, StringLength(11)] string DebtorBic,
    [property: Required, StringLength(140)] string CreditorName,
    [property: Required, StringLength(34)] string CreditorIban,
    [property: Required, StringLength(11)] string CreditorBic,
    string? RemittanceInformation);
