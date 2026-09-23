using MediatR;

namespace AzureSuite.Catalog.Application.Routes.Commands.DeleteRoute
{
    public record DeleteRouteCommand(Guid Id) : IRequest;
}
