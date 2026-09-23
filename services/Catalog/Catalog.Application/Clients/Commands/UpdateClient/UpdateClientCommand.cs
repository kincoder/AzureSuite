using MediatR;

namespace AzureSuite.Catalog.Application.Clients.Commands.UpdateClient
{
    public record UpdateClientCommand(Guid Id, string Name) : IRequest<ClientDto>;
}
