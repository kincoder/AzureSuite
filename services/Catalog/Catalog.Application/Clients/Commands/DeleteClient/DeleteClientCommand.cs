using MediatR;

namespace AzureSuite.Catalog.Application.Clients.Commands.DeleteClient
{
    public record DeleteClientCommand(Guid Id) : IRequest;
}
