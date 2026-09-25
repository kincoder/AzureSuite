namespace AzureSuite.Catalog.Api.Contracts
{
    public record UpdateRouteRequest(Guid ClientId, string MessageTypeName, string MessageTypeVersion, IReadOnlyList<string> QueueNames);
}
