using AzureSuite.Catalog.Application.Abstractions;
using AzureSuite.Catalog.Domain.ValueObjects;
using MediatR;

namespace AzureSuite.Catalog.Application.Routes.Queries.LookupRoute
{
    public class LookupRouteHandler : IRequestHandler<LookupRouteQuery, LookupRouteResultDto>
    {
        private readonly IRouteRepository _repository;

        public LookupRouteHandler(IRouteRepository repository)
        {
            _repository = repository;
        }

        public async Task<LookupRouteResultDto> Handle(LookupRouteQuery request, CancellationToken cancellationToken)
        {
            var routes = await _repository.ListByClientAndTypeAsync(
                request.ClientId,
                new MessageTypeName(request.MessageTypeName),
                new MessageTypeVersion(request.MessageTypeVersion),
                cancellationToken);

            var queueNames = routes.SelectMany(r => r.QueueNames).Distinct().ToList();
            return new LookupRouteResultDto(queueNames);
        }
    }
}
