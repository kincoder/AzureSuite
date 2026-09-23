using AzureSuite.Catalog.Application.Abstractions;
using AzureSuite.Catalog.Domain.Entities;
using AzureSuite.Catalog.Domain.ValueObjects;
using MediatR;

namespace AzureSuite.Catalog.Application.Routes.Commands.UpdateRoute
{
    public class UpdateRouteHandler : IRequestHandler<UpdateRouteCommand, RouteDto>
    {
        private readonly IRouteRepository _repository;

        public UpdateRouteHandler(IRouteRepository repository)
        {
            _repository = repository;
        }

        public async Task<RouteDto> Handle(UpdateRouteCommand request, CancellationToken cancellationToken)
        {
            var existing = await _repository.GetByIdAsync(request.Id, cancellationToken);
            if (existing is null)
            {
                throw new InvalidOperationException($"Route '{request.Id}' was not found.");
            }

            // Run the new values through Route's constructor validation without persisting the
            // throwaway Id it mints -- the repository is updated by existing.Id, not this one.
            var messageTypeName = new MessageTypeName(request.MessageTypeName);
            var messageTypeVersion = new MessageTypeVersion(request.MessageTypeVersion);
            var validated = new Route(request.ClientId, messageTypeName, messageTypeVersion, request.QueueNames);

            await _repository.UpdateAsync(existing.Id, validated.ClientId, validated.MessageTypeName, validated.MessageTypeVersion, validated.QueueNames, cancellationToken);

            return new RouteDto(existing.Id, validated.ClientId, validated.MessageTypeName.Value, validated.MessageTypeVersion.Value, validated.QueueNames);
        }
    }
}
