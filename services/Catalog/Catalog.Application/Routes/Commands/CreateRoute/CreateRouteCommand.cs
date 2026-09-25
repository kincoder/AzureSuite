using MediatR;

namespace AzureSuite.Catalog.Application.Routes.Commands.CreateRoute
{
    public record CreateRouteCommand(Guid ClientId, string MessageTypeName, string MessageTypeVersion, IReadOnlyList<string> QueueNames) : IRequest<RouteDto>;
}
