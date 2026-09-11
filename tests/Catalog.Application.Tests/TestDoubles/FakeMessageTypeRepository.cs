using AzureSuite.Catalog.Application.Abstractions;
using AzureSuite.Catalog.Domain.Entities;

namespace Catalog.Application.Tests.TestDoubles;

public class FakeMessageTypeRepository : IMessageTypeRepository
{
    private readonly List<MessageType> _messageTypes = new();

    public Task AddAsync(MessageType messageType, CancellationToken cancellationToken)
    {
        _messageTypes.Add(messageType);
        return Task.CompletedTask;
    }

    public Task<MessageType?> GetByNameAndVersionAsync(string name, string version, CancellationToken cancellationToken)
    {
        var found = _messageTypes.FirstOrDefault(m => m.Name == name && m.Version == version);
        return Task.FromResult(found);
    }

    public Task<IReadOnlyList<MessageType>> ListAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<MessageType>>(_messageTypes.ToList());
    }
}
