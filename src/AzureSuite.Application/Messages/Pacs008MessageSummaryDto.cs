namespace AzureSuite.Application.Messages;

/// <summary>
/// API/UI-facing projection of a message - decouples callers from the Domain entity
/// shape, so the entity is free to evolve without breaking the API contract.
/// </summary>
public record Pacs008MessageSummaryDto(
    Guid Id,
    string MessageId,
    string EndToEndId,
    decimal Amount,
    string Currency,
    string Status);
