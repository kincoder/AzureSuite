using AzureSuite.Catalog.Application.Abstractions;
using MediatR;

namespace AzureSuite.Catalog.Application.Clients.Queries.GetClient
{
    public class GetClientHandler : IRequestHandler<GetClientQuery, ClientDto?>
    {
        private readonly IClientRepository _repository;

        public GetClientHandler(IClientRepository repository)
        {
            _repository = repository;
        }

        public async Task<ClientDto?> Handle(GetClientQuery request, CancellationToken cancellationToken)
        {
            var client = await _repository.GetByIdAsync(request.Id, cancellationToken);
            return client is null ? null : new ClientDto(client.Id, client.Name, client.RegisteredAtUtc);
        }
    }
}
