using MediatR;

namespace AzureSuite.Catalog.Application.Routes.Commands.UpdateRoute
{
    public record UpdateRouteCommand(Guid Id, Guid ClientId, string MessageTypeName, string MessageTypeVersion, IReadOnlyList<string> QueueNames) : IRequest<RouteDto>;
}
