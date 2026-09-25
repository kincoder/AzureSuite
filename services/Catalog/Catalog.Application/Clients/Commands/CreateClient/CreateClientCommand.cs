using MediatR;

namespace AzureSuite.Catalog.Application.Clients.Commands.CreateClient
{
    public record CreateClientCommand(string Name) : IRequest<ClientDto>;
}
