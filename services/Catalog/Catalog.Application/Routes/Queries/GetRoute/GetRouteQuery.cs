using MediatR;

namespace AzureSuite.Catalog.Application.Routes.Queries.GetRoute
{
    public record GetRouteQuery(Guid Id) : IRequest<RouteDto?>;
}
