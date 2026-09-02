using AzureSuite.Application.Abstractions;
using AzureSuite.Domain.Entities;

namespace AzureSuite.Application.Tests.TestDoubles;

/// <summary>
/// Simple in-memory test double for IPacs008MessageRepository - keeps the Application
/// test suite free of any real persistence dependency (EF Core, a mocking library, etc.)
/// while still exercising Pacs008MessageService's actual logic.
/// </summary>
public class FakePacs008MessageRepository : IPacs008MessageRepository
{
    private readonly List<Pacs008Message> _messages = [];

    public IReadOnlyList<Pacs008Message> AddedMessages => _messages;

    public Task<IReadOnlyList<Pacs008Message>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Pacs008Message>>(_messages.ToList());

    public Task<Pacs008Message?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_messages.FirstOrDefault(m => m.Id == id));

    public Task AddAsync(Pacs008Message message, CancellationToken cancellationToken = default)
    {
        _messages.Add(message);
        return Task.CompletedTask;
    }
}
