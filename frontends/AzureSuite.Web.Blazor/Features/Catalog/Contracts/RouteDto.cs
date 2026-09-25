namespace AzureSuite.Web.Blazor.Features.Catalog.Contracts
{
    public record RouteDto(Guid Id, Guid ClientId, string MessageTypeName, string MessageTypeVersion, IReadOnlyList<string> QueueNames);
}
