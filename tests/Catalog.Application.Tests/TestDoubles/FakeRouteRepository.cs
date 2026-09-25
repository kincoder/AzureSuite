using AzureSuite.Catalog.Application.Abstractions;
using AzureSuite.Catalog.Domain.Entities;
using AzureSuite.Catalog.Domain.ValueObjects;

namespace Catalog.Application.Tests.TestDoubles
{
    public class FakeRouteRepository : IRouteRepository
    {
        private readonly List<Route> _routes = new();

        public Task AddAsync(Route route, CancellationToken cancellationToken)
        {
            _routes.Add(route);
            return Task.CompletedTask;
        }

        public Task<Route?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            return Task.FromResult(_routes.FirstOrDefault(r => r.Id == id));
        }

        public Task<IReadOnlyList<Route>> ListAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<Route>>(_routes.ToList());
        }

        public Task<IReadOnlyList<Route>> ListByClientAndTypeAsync(Guid clientId, MessageTypeName messageTypeName, MessageTypeVersion messageTypeVersion, CancellationToken cancellationToken)
        {
            var matches = _routes.Where(r => r.ClientId == clientId && r.MessageTypeName == messageTypeName && r.MessageTypeVersion == messageTypeVersion).ToList();
            return Task.FromResult<IReadOnlyList<Route>>(matches);
        }

        public Task UpdateAsync(Guid id, Guid clientId, MessageTypeName messageTypeName, MessageTypeVersion messageTypeVersion, IReadOnlyList<string> queueNames, CancellationToken cancellationToken)
        {
            var index = _routes.FindIndex(r => r.Id == id);
            if (index >= 0)
            {
                var replacement = new Route(clientId, messageTypeName, messageTypeVersion, queueNames);
                typeof(Route).GetField("<Id>k__BackingField", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(replacement, id);
                _routes[index] = replacement;
            }
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken)
        {
            _routes.RemoveAll(r => r.Id == id);
            return Task.CompletedTask;
        }
    }
}
