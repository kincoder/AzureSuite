using AzureSuite.Catalog.Application.Abstractions;
using AzureSuite.Catalog.Domain.Entities;

namespace Catalog.Application.Tests.TestDoubles
{
    public class FakeClientRepository : IClientRepository
    {
        private readonly List<Client> _clients = new();

        public Task AddAsync(Client client, CancellationToken cancellationToken)
        {
            _clients.Add(client);
            return Task.CompletedTask;
        }

        public Task<Client?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            return Task.FromResult(_clients.FirstOrDefault(c => c.Id == id));
        }

        public Task<IReadOnlyList<Client>> ListAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<Client>>(_clients.ToList());
        }

        public Task UpdateAsync(Guid id, string name, CancellationToken cancellationToken)
        {
            var index = _clients.FindIndex(c => c.Id == id);
            if (index >= 0)
            {
                // Client is a get-only-property entity with no setter at all (not even
                // private), so this fake replaces the list entry outright. It builds the
                // replacement via the public constructor for validation, then overwrites the
                // backing fields for Id/RegisteredAtUtc so identity is preserved -- mirroring
                // what the real EF repository achieves via CurrentValues.SetValues.
                var replacement = new Client(name);
                typeof(Client).GetField("<Id>k__BackingField", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(replacement, id);
                typeof(Client).GetField("<RegisteredAtUtc>k__BackingField", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(replacement, _clients[index].RegisteredAtUtc);
                _clients[index] = replacement;
            }
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken)
        {
            _clients.RemoveAll(c => c.Id == id);
            return Task.CompletedTask;
        }
    }
}
