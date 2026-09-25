using MediatR;

namespace AzureSuite.Catalog.Application.Clients.Queries.ListClients
{
    public record ListClientsQuery : IRequest<IReadOnlyList<ClientDto>>;
}
