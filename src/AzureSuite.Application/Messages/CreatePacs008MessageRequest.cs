using System.ComponentModel.DataAnnotations;

namespace AzureSuite.Application.Messages;

/// <summary>
/// Input shape for submitting a new PACS.008 message. Field length limits mirror the
/// ISO 20022 pacs.008 schema (e.g. MessageId/EndToEndId max 35 chars) so invalid input
/// is rejected with a clean validation error instead of surfacing as a raw SQL
/// truncation error.
///
/// Deliberately a plain mutable class, not a record: this same type is bound directly
/// by both the API's [ApiController] model binding AND the Blazor Web UI's EditForm
/// two-way binding (@bind-Value), which requires ordinary settable properties - a
/// record's default init-only properties can't be assigned to from Blazor's generated
/// binding code. One shared type between the two layers means no duplicate
/// "NewMessageModel"-style shadow class in the UI.
/// </summary>
public class CreatePacs008MessageRequest
{
    [Required, StringLength(35)]
    public string MessageId { get; set; } = "";

    [Required, StringLength(35)]
    public string EndToEndId { get; set; } = "";

    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }

    [Required, StringLength(3, MinimumLength = 3)]
    public string Currency { get; set; } = "";

    [Required, StringLength(140)]
    public string DebtorName { get; set; } = "";

    [Required, StringLength(34)]
    public string DebtorIban { get; set; } = "";

    [Required, StringLength(11)]
    public string DebtorBic { get; set; } = "";

    [Required, StringLength(140)]
    public string CreditorName { get; set; } = "";

    [Required, StringLength(34)]
    public string CreditorIban { get; set; } = "";

    [Required, StringLength(11)]
    public string CreditorBic { get; set; } = "";

    public string? RemittanceInformation { get; set; }
}
