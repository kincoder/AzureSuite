namespace AzureSuite.Web.Blazor.Features.Catalog.Contracts
{
    // Only ever constructed at submit time from the page's own bound fields (see
    // RegisterRoute.razor.cs) -- never itself a two-way @bind-Value target, so a record
    // (init-only) is fine here, unlike CreateClientRequest.
    public record CreateRouteRequest(Guid ClientId, string MessageTypeName, string MessageTypeVersion, IReadOnlyList<string> QueueNames);
}
