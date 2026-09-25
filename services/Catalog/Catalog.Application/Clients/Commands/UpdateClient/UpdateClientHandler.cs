using AzureSuite.Catalog.Application.Abstractions;
using AzureSuite.Catalog.Domain.Entities;
using MediatR;

namespace AzureSuite.Catalog.Application.Clients.Commands.UpdateClient
{
    public class UpdateClientHandler : IRequestHandler<UpdateClientCommand, ClientDto>
    {
        private readonly IClientRepository _repository;

        public UpdateClientHandler(IClientRepository repository)
        {
            _repository = repository;
        }

        public async Task<ClientDto> Handle(UpdateClientCommand request, CancellationToken cancellationToken)
        {
            var existing = await _repository.GetByIdAsync(request.Id, cancellationToken);
            if (existing is null)
            {
                throw new InvalidOperationException($"Client '{request.Id}' was not found.");
            }

            // Run the new name through Client's constructor validation without persisting the
            // throwaway Id it mints — the repository is updated by existing.Id, not this one.
            var validated = new Client(request.Name);

            await _repository.UpdateAsync(existing.Id, validated.Name, cancellationToken);
            return new ClientDto(existing.Id, validated.Name, existing.RegisteredAtUtc);
        }
    }
}
