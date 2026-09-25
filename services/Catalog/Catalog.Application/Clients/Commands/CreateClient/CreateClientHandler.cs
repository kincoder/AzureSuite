using AzureSuite.Catalog.Application.Abstractions;
using AzureSuite.Catalog.Domain.Entities;
using MediatR;

namespace AzureSuite.Catalog.Application.Clients.Commands.CreateClient
{
    public class CreateClientHandler : IRequestHandler<CreateClientCommand, ClientDto>
    {
        private readonly IClientRepository _repository;

        public CreateClientHandler(IClientRepository repository)
        {
            _repository = repository;
        }

        public async Task<ClientDto> Handle(CreateClientCommand request, CancellationToken cancellationToken)
        {
            var client = new Client(request.Name);
            await _repository.AddAsync(client, cancellationToken);
            return new ClientDto(client.Id, client.Name, client.RegisteredAtUtc);
        }
    }
}
