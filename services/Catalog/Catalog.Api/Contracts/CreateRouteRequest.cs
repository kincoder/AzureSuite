namespace AzureSuite.Catalog.Api.Contracts
{
    public record CreateRouteRequest(Guid ClientId, string MessageTypeName, string MessageTypeVersion, IReadOnlyList<string> QueueNames);
}
