using AzureSuite.Catalog.Application.Abstractions;
using AzureSuite.Catalog.Domain.Entities;
using AzureSuite.Catalog.Domain.ValueObjects;
using MediatR;

namespace AzureSuite.Catalog.Application.Routes.Commands.CreateRoute
{
    public class CreateRouteHandler : IRequestHandler<CreateRouteCommand, RouteDto>
    {
        private readonly IRouteRepository _repository;

        public CreateRouteHandler(IRouteRepository repository)
        {
            _repository = repository;
        }

        public async Task<RouteDto> Handle(CreateRouteCommand request, CancellationToken cancellationToken)
        {
            var route = new Route(
                request.ClientId,
                new MessageTypeName(request.MessageTypeName),
                new MessageTypeVersion(request.MessageTypeVersion),
                request.QueueNames);

            await _repository.AddAsync(route, cancellationToken);

            return new RouteDto(route.Id, route.ClientId, route.MessageTypeName.Value, route.MessageTypeVersion.Value, route.QueueNames);
        }
    }
}
