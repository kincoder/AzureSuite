using AzureSuite.Catalog.Application.Abstractions;
using MediatR;

namespace AzureSuite.Catalog.Application.Clients.Commands.DeleteClient
{
    public class DeleteClientHandler : IRequestHandler<DeleteClientCommand>
    {
        private readonly IClientRepository _repository;

        public DeleteClientHandler(IClientRepository repository)
        {
            _repository = repository;
        }

        public async Task Handle(DeleteClientCommand request, CancellationToken cancellationToken)
        {
            await _repository.DeleteAsync(request.Id, cancellationToken);
        }
    }
}
