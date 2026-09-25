using MediatR;

namespace AzureSuite.Catalog.Application.Clients.Queries.GetClient
{
    public record GetClientQuery(Guid Id) : IRequest<ClientDto?>;
}
