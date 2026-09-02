namespace AzureSuite.Application.Messages;

/// <summary>Input shape for submitting a new PACS.008 message.</summary>
public record CreatePacs008MessageRequest(
    string MessageId,
    string EndToEndId,
    decimal Amount,
    string Currency,
    string DebtorName,
    string DebtorIban,
    string DebtorBic,
    string CreditorName,
    string CreditorIban,
    string CreditorBic,
    string? RemittanceInformation);
