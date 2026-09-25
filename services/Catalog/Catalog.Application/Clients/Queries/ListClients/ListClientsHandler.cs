using AzureSuite.Catalog.Application.Abstractions;
using MediatR;

namespace AzureSuite.Catalog.Application.Clients.Queries.ListClients
{
    public class ListClientsHandler : IRequestHandler<ListClientsQuery, IReadOnlyList<ClientDto>>
    {
        private readonly IClientRepository _repository;

        public ListClientsHandler(IClientRepository repository)
        {
            _repository = repository;
        }

        public async Task<IReadOnlyList<ClientDto>> Handle(ListClientsQuery request, CancellationToken cancellationToken)
        {
            var clients = await _repository.ListAsync(cancellationToken);
            return clients.Select(c => new ClientDto(c.Id, c.Name, c.RegisteredAtUtc)).ToList();
        }
    }
}
