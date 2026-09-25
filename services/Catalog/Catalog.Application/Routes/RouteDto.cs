namespace AzureSuite.Catalog.Application.Routes
{
    public record RouteDto(Guid Id, Guid ClientId, string MessageTypeName, string MessageTypeVersion, IReadOnlyList<string> QueueNames);
}
