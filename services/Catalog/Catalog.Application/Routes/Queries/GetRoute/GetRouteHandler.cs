using AzureSuite.Catalog.Application.Abstractions;
using MediatR;

namespace AzureSuite.Catalog.Application.Routes.Queries.GetRoute
{
    public class GetRouteHandler : IRequestHandler<GetRouteQuery, RouteDto?>
    {
        private readonly IRouteRepository _repository;

        public GetRouteHandler(IRouteRepository repository)
        {
            _repository = repository;
        }

        public async Task<RouteDto?> Handle(GetRouteQuery request, CancellationToken cancellationToken)
        {
            var route = await _repository.GetByIdAsync(request.Id, cancellationToken);
            return route is null ? null : new RouteDto(route.Id, route.ClientId, route.MessageTypeName.Value, route.MessageTypeVersion.Value, route.QueueNames);
        }
    }
}
