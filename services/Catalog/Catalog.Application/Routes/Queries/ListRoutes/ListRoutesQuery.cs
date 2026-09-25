using MediatR;

namespace AzureSuite.Catalog.Application.Routes.Queries.ListRoutes
{
    public record ListRoutesQuery : IRequest<IReadOnlyList<RouteDto>>;
}
