namespace AzureSuite.Domain.ValueObjects;

/// <summary>
/// The debtor or creditor side of a payment: who they are and where the money moves
/// from/to. Modeled as an EF Core owned type, not its own entity - it has no identity
/// or lifecycle independent of the message it belongs to.
/// </summary>
public class PartyAccount
{
    public required string Name { get; set; }
    public required string Iban { get; set; }
    public required string BicCode { get; set; }
}
