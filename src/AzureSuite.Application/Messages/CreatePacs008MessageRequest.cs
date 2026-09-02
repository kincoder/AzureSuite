using System.ComponentModel.DataAnnotations;

namespace AzureSuite.Application.Messages;

/// <summary>
/// Input shape for submitting a new PACS.008 message. Field length limits mirror the
/// ISO 20022 pacs.008 schema (e.g. MessageId/EndToEndId max 35 chars) so invalid input
/// is rejected with a clean 400 here, instead of surfacing as a raw SQL truncation error.
/// Attributes must target the constructor parameters directly (no [property: ...]) -
/// ASP.NET Core's model validation reads metadata from record primary constructor
/// parameters, not from the generated properties, and throws if it finds validation
/// attributes attached to the properties instead.
/// </summary>
public record CreatePacs008MessageRequest(
    [Required, StringLength(35)] string MessageId,
    [Required, StringLength(35)] string EndToEndId,
    [Range(0.01, double.MaxValue)] decimal Amount,
    [Required, StringLength(3, MinimumLength = 3)] string Currency,
    [Required, StringLength(140)] string DebtorName,
    [Required, StringLength(34)] string DebtorIban,
    [Required, StringLength(11)] string DebtorBic,
    [Required, StringLength(140)] string CreditorName,
    [Required, StringLength(34)] string CreditorIban,
    [Required, StringLength(11)] string CreditorBic,
    string? RemittanceInformation);
