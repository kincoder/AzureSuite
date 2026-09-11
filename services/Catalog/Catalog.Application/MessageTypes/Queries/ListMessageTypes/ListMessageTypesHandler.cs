using AzureSuite.Catalog.Application.Abstractions;
using MediatR;

namespace AzureSuite.Catalog.Application.MessageTypes.Queries.ListMessageTypes
{
    public class ListMessageTypesHandler : IRequestHandler<ListMessageTypesQuery, IReadOnlyList<MessageTypeDto>>
    {
        private readonly IMessageTypeRepository _repository;

        public ListMessageTypesHandler(IMessageTypeRepository repository)
        {
            _repository = repository;
        }

        public async Task<IReadOnlyList<MessageTypeDto>> Handle(ListMessageTypesQuery request, CancellationToken cancellationToken)
        {
            var messageTypes = await _repository.ListAsync(cancellationToken);
            return messageTypes
                .Select(m => new MessageTypeDto(m.Id, m.Name.Value, m.Version.Value, m.SchemaDefinition, m.RegisteredAtUtc))
                .ToList();
        }
    }
}
