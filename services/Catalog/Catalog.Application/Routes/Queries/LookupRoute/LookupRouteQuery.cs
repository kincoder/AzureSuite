using MediatR;

namespace AzureSuite.Catalog.Application.Routes.Queries.LookupRoute
{
    /// <summary>Checks only whether ClientId is authorized to send MessageTypeName/Version, and
    /// if so, which queues it's routed to. Knows nothing about payload shape or schemas.</summary>
    public record LookupRouteQuery(Guid ClientId, string MessageTypeName, string MessageTypeVersion) : IRequest<LookupRouteResultDto>;

    public record LookupRouteResultDto(IReadOnlyList<string> QueueNames);
}
