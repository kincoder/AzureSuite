using AzureSuite.Catalog.Application.Abstractions;
using MediatR;

namespace AzureSuite.Catalog.Application.Routes.Commands.DeleteRoute
{
    public class DeleteRouteHandler : IRequestHandler<DeleteRouteCommand>
    {
        private readonly IRouteRepository _repository;

        public DeleteRouteHandler(IRouteRepository repository)
        {
            _repository = repository;
        }

        public async Task Handle(DeleteRouteCommand request, CancellationToken cancellationToken)
        {
            await _repository.DeleteAsync(request.Id, cancellationToken);
        }
    }
}
