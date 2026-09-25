using AzureSuite.Catalog.Application.Abstractions;
using MediatR;

namespace AzureSuite.Catalog.Application.Routes.Queries.ListRoutes
{
    public class ListRoutesHandler : IRequestHandler<ListRoutesQuery, IReadOnlyList<RouteDto>>
    {
        private readonly IRouteRepository _repository;

        public ListRoutesHandler(IRouteRepository repository)
        {
            _repository = repository;
        }

        public async Task<IReadOnlyList<RouteDto>> Handle(ListRoutesQuery request, CancellationToken cancellationToken)
        {
            var routes = await _repository.ListAsync(cancellationToken);
            return routes.Select(r => new RouteDto(r.Id, r.ClientId, r.MessageTypeName.Value, r.MessageTypeVersion.Value, r.QueueNames)).ToList();
        }
    }
}
