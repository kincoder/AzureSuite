using AzureSuite.Catalog.Application.Abstractions;
using AzureSuite.Catalog.Application.Common;
using AzureSuite.Catalog.Domain.Entities;
using AzureSuite.Catalog.Domain.ValueObjects;
using MediatR;

namespace AzureSuite.Catalog.Application.Routes.Commands.CreateRoute
{
    public class CreateRouteHandler : IRequestHandler<CreateRouteCommand, RouteDto>
    {
        private readonly IRouteRepository _repository;
        private readonly IClientRepository _clientRepository;
        private readonly IMessageTypeRepository _messageTypeRepository;

        public CreateRouteHandler(IRouteRepository repository, IClientRepository clientRepository, IMessageTypeRepository messageTypeRepository)
        {
            _repository = repository;
            _clientRepository = clientRepository;
            _messageTypeRepository = messageTypeRepository;
        }

        public async Task<RouteDto> Handle(CreateRouteCommand request, CancellationToken cancellationToken)
        {
            var messageTypeName = new MessageTypeName(request.MessageTypeName);
            var messageTypeVersion = new MessageTypeVersion(request.MessageTypeVersion);

            if (await _clientRepository.GetByIdAsync(request.ClientId, cancellationToken) is null)
            {
                throw new ReferencedEntityNotFoundException($"Client '{request.ClientId}' does not exist.");
            }

            if (await _messageTypeRepository.GetByNameAndVersionAsync(messageTypeName, messageTypeVersion, cancellationToken) is null)
            {
                throw new ReferencedEntityNotFoundException($"Message type '{messageTypeName.Value}' version '{messageTypeVersion.Value}' is not registered.");
            }

            var route = new Route(request.ClientId, messageTypeName, messageTypeVersion, request.QueueNames);

            await _repository.AddAsync(route, cancellationToken);

            return new RouteDto(route.Id, route.ClientId, route.MessageTypeName.Value, route.MessageTypeVersion.Value, route.QueueNames);
        }
    }
}
