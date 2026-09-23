# Catalog Client & Route Management Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Enhance `Catalog.Api` with `Client` and `Route` entities, full CRUD for both, a schema-validation endpoint, and a route-lookup endpoint — the data and query surface the (separately planned) Processing pipeline will call.

**Architecture:** Same CQRS-over-MediatR, Domain/Application/Infrastructure/Api layering `MessageType` already uses in this codebase. `Client` and `Route` are new entities in `Catalog.Domain`, with repositories in `Catalog.Infrastructure` and command/query handlers in `Catalog.Application`. No new projects — everything lands inside the existing `Catalog.*` projects. UI additions go into the existing `AzureSuite.Web.Blazor` app's `Features/Catalog` area.

**Tech Stack:** ASP.NET Core minimal APIs, MediatR, EF Core (SQL Server / InMemory for local dev), `JsonSchema.Net` (new package, for the validate endpoint), Blazor WebAssembly, xUnit + FluentAssertions + bUnit.

**Spec:** `docs/superpowers/specs/2026-09-23-routing-processing-design.md` (Catalog enhancements section)

## Global Constraints

- Warning-free build, `Nullable` enabled, no `#pragma warning disable` except EF migration files.
- 1:1 `tests/X.Y.Tests` project per source project, mirroring folder structure, one test class per production class.
- xUnit + FluentAssertions; hand-written fakes (see `FakeMessageTypeRepository`) before reaching for a mocking library.
- Entities use constructor-only property assignment (no setters) with a single public constructor — EF Core materializes via constructor binding, exactly as `MessageType` already does. No parameterless constructors added for EF's sake.
- Every new command/query handler follows the existing `MessageTypes/{Commands,Queries}/<Name>/<Name>{Command,Query,Handler}.cs` folder shape.
- `Route.QueueNames` is `IReadOnlyList<string>`, mapped via EF Core's native primitive-collection support (a JSON column) — the simplest option that's still fully EF-idiomatic for a small string list, chosen over a hand-rolled join table for this scale.

---

## File Structure

```
services/Catalog/
  Catalog.Domain/
    Entities/
      Client.cs                                    (new)
      Route.cs                                      (new)
  Catalog.Application/
    Abstractions/
      IClientRepository.cs                          (new)
      IRouteRepository.cs                            (new)
    Clients/
      ClientDto.cs                                   (new)
      Commands/{CreateClient,UpdateClient,DeleteClient}/...   (new)
      Queries/{GetClient,ListClients}/...             (new)
    Routes/
      RouteDto.cs                                     (new)
      Commands/{CreateRoute,UpdateRoute,DeleteRoute}/...      (new)
      Queries/{GetRoute,ListRoutes,LookupRoute}/...   (new)
    MessageTypes/
      Queries/ValidateMessage/
        ValidateMessageQuery.cs, ValidateMessageHandler.cs, ValidationResultDto.cs   (new)
  Catalog.Infrastructure/
    Persistence/
      CatalogDbContext.cs                            (modify: add Client/Route DbSets + mapping)
      Repositories/
        ClientRepository.cs                          (new)
        RouteRepository.cs                            (new)
      Migrations/                                     (new migration)
  Catalog.Api/
    Contracts/
      CreateClientRequest.cs, UpdateClientRequest.cs   (new)
      CreateRouteRequest.cs, UpdateRouteRequest.cs      (new)
      ValidateMessageRequest.cs                         (new)
    Program.cs                                         (modify: register repos, map new endpoints)
    Persistence/InMemorySeedData.cs                     (modify: seed a couple of Clients/Routes)

frontends/AzureSuite.Web.Blazor/Features/Catalog/
  Contracts/
    ClientDto.cs, CreateClientRequest.cs, UpdateClientRequest.cs           (new)
    RouteDto.cs, CreateRouteRequest.cs, UpdateRouteRequest.cs               (new)
  Services/CatalogApiClient.cs                          (modify: add Client/Route methods)
  Pages/
    ClientsList.razor / .razor.cs                        (new)
    RegisterClient.razor / .razor.cs                      (new)
    RoutesList.razor / .razor.cs                           (new)
    RegisterRoute.razor / .razor.cs                        (new)
  Layout/NavMenu.razor                                   (modify: add Clients/Routes links)

tests/
  Catalog.Application.Tests/TestDoubles/FakeClientRepository.cs, FakeRouteRepository.cs   (new)
  Catalog.Application.Tests/Clients/..., Routes/..., MessageTypes/Queries/ValidateMessage/...  (new)
  Catalog.Infrastructure.Tests/Persistence/Repositories/ClientRepositoryTests.cs, RouteRepositoryTests.cs  (new)
  AzureSuite.Web.Blazor.Tests/Features/Catalog/Pages/ClientsListTests.cs, RoutesListTests.cs   (new)

scripts/seed-routing-test-data.sql                       (new)
```

---

### Task 1: `Client` domain entity + persistence

**Files:**
- Create: `services/Catalog/Catalog.Domain/Entities/Client.cs`
- Modify: `services/Catalog/Catalog.Infrastructure/Persistence/CatalogDbContext.cs`
- Test: `tests/Catalog.Domain.Tests/Entities/ClientTests.cs`

**Interfaces:**
- Produces: `Client(string name)` constructor; `Client.Id : Guid`, `Client.Name : string`, `Client.RegisteredAtUtc : DateTime`.

- [ ] **Step 1: Write the failing test**

```csharp
// tests/Catalog.Domain.Tests/Entities/ClientTests.cs
using AzureSuite.Catalog.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Catalog.Domain.Tests.Entities
{
    public class ClientTests
    {
        [Fact]
        public void Constructor_WithValidName_SetsProperties()
        {
            var client = new Client("Acme Corp");

            client.Id.Should().NotBeEmpty();
            client.Name.Should().Be("Acme Corp");
            client.RegisteredAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_WithMissingName_ThrowsArgumentException(string? name)
        {
            var act = () => new Client(name!);

            act.Should().Throw<ArgumentException>();
        }
    }
}
```

If `tests/Catalog.Domain.Tests` does not exist yet, create it mirroring `Catalog.Domain`'s shape (`Microsoft.NET.Sdk`, xUnit + FluentAssertions packages, a `ProjectReference` to `Catalog.Domain`), and add it to `AzureSuite.slnx`.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Catalog.Domain.Tests --filter ClientTests`
Expected: FAIL — `Client` does not exist.

- [ ] **Step 3: Write minimal implementation**

```csharp
// services/Catalog/Catalog.Domain/Entities/Client.cs
namespace AzureSuite.Catalog.Domain.Entities
{
    /// <summary>A registered sender identity. A <see cref="Route"/> ties a Client to the
    /// message types it's authorized to send.</summary>
    public class Client
    {
        public Guid Id { get; }

        public string Name { get; }

        public DateTime RegisteredAtUtc { get; }

        public Client(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Client name is required.", nameof(name));
            }

            Id = Guid.NewGuid();
            Name = name;
            RegisteredAtUtc = DateTime.UtcNow;
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/Catalog.Domain.Tests --filter ClientTests`
Expected: PASS

- [ ] **Step 5: Add `Client` to `CatalogDbContext`**

```csharp
// services/Catalog/Catalog.Infrastructure/Persistence/CatalogDbContext.cs
// Add alongside the existing MessageTypes DbSet:
public DbSet<Client> Clients => Set<Client>();
```

```csharp
// Inside OnModelCreating, alongside the existing MessageType configuration:
modelBuilder.Entity<Client>(entity =>
{
    entity.HasKey(c => c.Id);
    entity.Property(c => c.Id).ValueGeneratedNever();
    entity.Property(c => c.Name).IsRequired().HasMaxLength(200);
    entity.Property(c => c.RegisteredAtUtc).IsRequired();
});
```

- [ ] **Step 6: Add and apply the EF Core migration**

Run: `dotnet ef migrations add AddClient --project services/Catalog/Catalog.Infrastructure --startup-project services/Catalog/Catalog.Api`
Expected: a new migration file under `Catalog.Infrastructure/Persistence/Migrations/` adding a `Clients` table.

- [ ] **Step 7: Commit**

```bash
git add services/Catalog/Catalog.Domain/Entities/Client.cs services/Catalog/Catalog.Infrastructure/Persistence/CatalogDbContext.cs services/Catalog/Catalog.Infrastructure/Persistence/Migrations/ tests/Catalog.Domain.Tests/
git commit -m "feat(catalog): add Client entity"
```

---

### Task 2: `Client` repository, CQRS handlers, and API endpoints

**Files:**
- Create: `services/Catalog/Catalog.Application/Abstractions/IClientRepository.cs`
- Create: `services/Catalog/Catalog.Application/Clients/ClientDto.cs`
- Create: `services/Catalog/Catalog.Application/Clients/Commands/CreateClient/{CreateClientCommand,CreateClientHandler}.cs`
- Create: `services/Catalog/Catalog.Application/Clients/Commands/UpdateClient/{UpdateClientCommand,UpdateClientHandler}.cs`
- Create: `services/Catalog/Catalog.Application/Clients/Commands/DeleteClient/{DeleteClientCommand,DeleteClientHandler}.cs`
- Create: `services/Catalog/Catalog.Application/Clients/Queries/GetClient/{GetClientQuery,GetClientHandler}.cs`
- Create: `services/Catalog/Catalog.Application/Clients/Queries/ListClients/{ListClientsQuery,ListClientsHandler}.cs`
- Create: `services/Catalog/Catalog.Infrastructure/Persistence/Repositories/ClientRepository.cs`
- Create: `services/Catalog/Catalog.Api/Contracts/{CreateClientRequest,UpdateClientRequest}.cs`
- Modify: `services/Catalog/Catalog.Api/Program.cs`
- Test: `tests/Catalog.Application.Tests/TestDoubles/FakeClientRepository.cs`
- Test: `tests/Catalog.Application.Tests/Clients/Commands/CreateClient/CreateClientHandlerTests.cs`
- Test: `tests/Catalog.Application.Tests/Clients/Commands/UpdateClient/UpdateClientHandlerTests.cs`
- Test: `tests/Catalog.Application.Tests/Clients/Commands/DeleteClient/DeleteClientHandlerTests.cs`
- Test: `tests/Catalog.Application.Tests/Clients/Queries/GetClient/GetClientHandlerTests.cs`
- Test: `tests/Catalog.Application.Tests/Clients/Queries/ListClients/ListClientsHandlerTests.cs`
- Test: `tests/Catalog.Infrastructure.Tests/Persistence/Repositories/ClientRepositoryTests.cs`

**Interfaces:**
- Consumes: `Client` from Task 1.
- Produces: `IClientRepository` with `AddAsync`, `GetByIdAsync`, `ListAsync`, `UpdateAsync`, `DeleteAsync`; `ClientDto(Guid Id, string Name, DateTime RegisteredAtUtc)`; endpoints `POST/GET/PUT/DELETE /clients[/{id}]`.

- [ ] **Step 1: Write the failing handler tests**

```csharp
// tests/Catalog.Application.Tests/TestDoubles/FakeClientRepository.cs
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

        public Task UpdateAsync(Client client, CancellationToken cancellationToken)
        {
            var index = _clients.FindIndex(c => c.Id == client.Id);
            if (index >= 0) _clients[index] = client;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken)
        {
            _clients.RemoveAll(c => c.Id == id);
            return Task.CompletedTask;
        }
    }
}
```

```csharp
// tests/Catalog.Application.Tests/Clients/Commands/CreateClient/CreateClientHandlerTests.cs
using AzureSuite.Catalog.Application.Clients.Commands.CreateClient;
using Catalog.Application.Tests.TestDoubles;
using FluentAssertions;
using Xunit;

namespace Catalog.Application.Tests.Clients.Commands.CreateClient
{
    public class CreateClientHandlerTests
    {
        [Fact]
        public async Task Handle_WithValidName_CreatesAndReturnsDto()
        {
            var repository = new FakeClientRepository();
            var handler = new CreateClientHandler(repository);

            var result = await handler.Handle(new CreateClientCommand("Acme Corp"), CancellationToken.None);

            result.Name.Should().Be("Acme Corp");
            result.Id.Should().NotBeEmpty();
            (await repository.GetByIdAsync(result.Id, CancellationToken.None)).Should().NotBeNull();
        }
    }
}
```

```csharp
// tests/Catalog.Application.Tests/Clients/Queries/GetClient/GetClientHandlerTests.cs
using AzureSuite.Catalog.Application.Clients.Queries.GetClient;
using AzureSuite.Catalog.Domain.Entities;
using Catalog.Application.Tests.TestDoubles;
using FluentAssertions;
using Xunit;

namespace Catalog.Application.Tests.Clients.Queries.GetClient
{
    public class GetClientHandlerTests
    {
        [Fact]
        public async Task Handle_WithExistingId_ReturnsDto()
        {
            var repository = new FakeClientRepository();
            var client = new Client("Acme Corp");
            await repository.AddAsync(client, CancellationToken.None);
            var handler = new GetClientHandler(repository);

            var result = await handler.Handle(new GetClientQuery(client.Id), CancellationToken.None);

            result.Should().NotBeNull();
            result!.Name.Should().Be("Acme Corp");
        }

        [Fact]
        public async Task Handle_WithUnknownId_ReturnsNull()
        {
            var handler = new GetClientHandler(new FakeClientRepository());

            var result = await handler.Handle(new GetClientQuery(Guid.NewGuid()), CancellationToken.None);

            result.Should().BeNull();
        }
    }
}
```

```csharp
// tests/Catalog.Application.Tests/Clients/Queries/ListClients/ListClientsHandlerTests.cs
using AzureSuite.Catalog.Application.Clients.Queries.ListClients;
using AzureSuite.Catalog.Domain.Entities;
using Catalog.Application.Tests.TestDoubles;
using FluentAssertions;
using Xunit;

namespace Catalog.Application.Tests.Clients.Queries.ListClients
{
    public class ListClientsHandlerTests
    {
        [Fact]
        public async Task Handle_ReturnsAllClients()
        {
            var repository = new FakeClientRepository();
            await repository.AddAsync(new Client("Acme Corp"), CancellationToken.None);
            await repository.AddAsync(new Client("Globex"), CancellationToken.None);
            var handler = new ListClientsHandler(repository);

            var result = await handler.Handle(new ListClientsQuery(), CancellationToken.None);

            result.Should().HaveCount(2);
        }
    }
}
```

```csharp
// tests/Catalog.Application.Tests/Clients/Commands/UpdateClient/UpdateClientHandlerTests.cs
using AzureSuite.Catalog.Application.Clients.Commands.UpdateClient;
using AzureSuite.Catalog.Domain.Entities;
using Catalog.Application.Tests.TestDoubles;
using FluentAssertions;
using Xunit;

namespace Catalog.Application.Tests.Clients.Commands.UpdateClient
{
    public class UpdateClientHandlerTests
    {
        [Fact]
        public async Task Handle_WithExistingId_UpdatesName()
        {
            var repository = new FakeClientRepository();
            var client = new Client("Acme Corp");
            await repository.AddAsync(client, CancellationToken.None);
            var handler = new UpdateClientHandler(repository);

            var result = await handler.Handle(new UpdateClientCommand(client.Id, "Acme Corporation"), CancellationToken.None);

            result.Name.Should().Be("Acme Corporation");
        }

        [Fact]
        public async Task Handle_WithUnknownId_ThrowsInvalidOperationException()
        {
            var handler = new UpdateClientHandler(new FakeClientRepository());

            var act = () => handler.Handle(new UpdateClientCommand(Guid.NewGuid(), "X"), CancellationToken.None);

            await act.Should().ThrowAsync<InvalidOperationException>();
        }
    }
}
```

```csharp
// tests/Catalog.Application.Tests/Clients/Commands/DeleteClient/DeleteClientHandlerTests.cs
using AzureSuite.Catalog.Application.Clients.Commands.DeleteClient;
using AzureSuite.Catalog.Domain.Entities;
using Catalog.Application.Tests.TestDoubles;
using FluentAssertions;
using Xunit;

namespace Catalog.Application.Tests.Clients.Commands.DeleteClient
{
    public class DeleteClientHandlerTests
    {
        [Fact]
        public async Task Handle_WithExistingId_RemovesClient()
        {
            var repository = new FakeClientRepository();
            var client = new Client("Acme Corp");
            await repository.AddAsync(client, CancellationToken.None);
            var handler = new DeleteClientHandler(repository);

            await handler.Handle(new DeleteClientCommand(client.Id), CancellationToken.None);

            (await repository.GetByIdAsync(client.Id, CancellationToken.None)).Should().BeNull();
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Catalog.Application.Tests --filter FullyQualifiedName~Clients`
Expected: FAIL — none of the referenced types exist yet.

- [ ] **Step 3: Write the abstraction, DTO, and handlers**

```csharp
// services/Catalog/Catalog.Application/Abstractions/IClientRepository.cs
using AzureSuite.Catalog.Domain.Entities;

namespace AzureSuite.Catalog.Application.Abstractions
{
    public interface IClientRepository
    {
        Task AddAsync(Client client, CancellationToken cancellationToken);
        Task<Client?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
        Task<IReadOnlyList<Client>> ListAsync(CancellationToken cancellationToken);
        Task UpdateAsync(Client client, CancellationToken cancellationToken);
        Task DeleteAsync(Guid id, CancellationToken cancellationToken);
    }
}
```

```csharp
// services/Catalog/Catalog.Application/Clients/ClientDto.cs
namespace AzureSuite.Catalog.Application.Clients
{
    public record ClientDto(Guid Id, string Name, DateTime RegisteredAtUtc);
}
```

```csharp
// services/Catalog/Catalog.Application/Clients/Commands/CreateClient/CreateClientCommand.cs
using MediatR;

namespace AzureSuite.Catalog.Application.Clients.Commands.CreateClient
{
    public record CreateClientCommand(string Name) : IRequest<ClientDto>;
}
```

```csharp
// services/Catalog/Catalog.Application/Clients/Commands/CreateClient/CreateClientHandler.cs
using AzureSuite.Catalog.Application.Abstractions;
using AzureSuite.Catalog.Domain.Entities;
using MediatR;

namespace AzureSuite.Catalog.Application.Clients.Commands.CreateClient
{
    public class CreateClientHandler : IRequestHandler<CreateClientCommand, ClientDto>
    {
        private readonly IClientRepository _repository;

        public CreateClientHandler(IClientRepository repository)
        {
            _repository = repository;
        }

        public async Task<ClientDto> Handle(CreateClientCommand request, CancellationToken cancellationToken)
        {
            var client = new Client(request.Name);
            await _repository.AddAsync(client, cancellationToken);
            return new ClientDto(client.Id, client.Name, client.RegisteredAtUtc);
        }
    }
}
```

```csharp
// services/Catalog/Catalog.Application/Clients/Queries/GetClient/GetClientQuery.cs
using MediatR;

namespace AzureSuite.Catalog.Application.Clients.Queries.GetClient
{
    public record GetClientQuery(Guid Id) : IRequest<ClientDto?>;
}
```

```csharp
// services/Catalog/Catalog.Application/Clients/Queries/GetClient/GetClientHandler.cs
using AzureSuite.Catalog.Application.Abstractions;
using MediatR;

namespace AzureSuite.Catalog.Application.Clients.Queries.GetClient
{
    public class GetClientHandler : IRequestHandler<GetClientQuery, ClientDto?>
    {
        private readonly IClientRepository _repository;

        public GetClientHandler(IClientRepository repository)
        {
            _repository = repository;
        }

        public async Task<ClientDto?> Handle(GetClientQuery request, CancellationToken cancellationToken)
        {
            var client = await _repository.GetByIdAsync(request.Id, cancellationToken);
            return client is null ? null : new ClientDto(client.Id, client.Name, client.RegisteredAtUtc);
        }
    }
}
```

```csharp
// services/Catalog/Catalog.Application/Clients/Queries/ListClients/ListClientsQuery.cs
using MediatR;

namespace AzureSuite.Catalog.Application.Clients.Queries.ListClients
{
    public record ListClientsQuery : IRequest<IReadOnlyList<ClientDto>>;
}
```

```csharp
// services/Catalog/Catalog.Application/Clients/Queries/ListClients/ListClientsHandler.cs
using AzureSuite.Catalog.Application.Abstractions;
using MediatR;

namespace AzureSuite.Catalog.Application.Clients.Queries.ListClients
{
    public class ListClientsHandler : IRequestHandler<ListClientsQuery, IReadOnlyList<ClientDto>>
    {
        private readonly IClientRepository _repository;

        public ListClientsHandler(IClientRepository repository)
        {
            _repository = repository;
        }

        public async Task<IReadOnlyList<ClientDto>> Handle(ListClientsQuery request, CancellationToken cancellationToken)
        {
            var clients = await _repository.ListAsync(cancellationToken);
            return clients.Select(c => new ClientDto(c.Id, c.Name, c.RegisteredAtUtc)).ToList();
        }
    }
}
```

```csharp
// services/Catalog/Catalog.Application/Clients/Commands/UpdateClient/UpdateClientCommand.cs
using MediatR;

namespace AzureSuite.Catalog.Application.Clients.Commands.UpdateClient
{
    public record UpdateClientCommand(Guid Id, string Name) : IRequest<ClientDto>;
}
```

```csharp
// services/Catalog/Catalog.Application/Clients/Commands/UpdateClient/UpdateClientHandler.cs
using AzureSuite.Catalog.Application.Abstractions;
using AzureSuite.Catalog.Domain.Entities;
using MediatR;

namespace AzureSuite.Catalog.Application.Clients.Commands.UpdateClient
{
    public class UpdateClientHandler : IRequestHandler<UpdateClientCommand, ClientDto>
    {
        private readonly IClientRepository _repository;

        public UpdateClientHandler(IClientRepository repository)
        {
            _repository = repository;
        }

        public async Task<ClientDto> Handle(UpdateClientCommand request, CancellationToken cancellationToken)
        {
            var existing = await _repository.GetByIdAsync(request.Id, cancellationToken);
            if (existing is null)
            {
                throw new InvalidOperationException($"Client '{request.Id}' was not found.");
            }

            var updated = new Client(request.Name);
            await _repository.UpdateAsync(updated, cancellationToken);
            return new ClientDto(existing.Id, updated.Name, existing.RegisteredAtUtc);
        }
    }
}
```

Note: `UpdateAsync` receives a freshly-constructed `Client` (new `Id`) purely to reuse the constructor's validation; the repository implementation (Step 5) must preserve `existing.Id`/`RegisteredAtUtc` when persisting, not the new instance's own `Id`.

```csharp
// services/Catalog/Catalog.Application/Clients/Commands/DeleteClient/DeleteClientCommand.cs
using MediatR;

namespace AzureSuite.Catalog.Application.Clients.Commands.DeleteClient
{
    public record DeleteClientCommand(Guid Id) : IRequest;
}
```

```csharp
// services/Catalog/Catalog.Application/Clients/Commands/DeleteClient/DeleteClientHandler.cs
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
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Catalog.Application.Tests --filter FullyQualifiedName~Clients`
Expected: PASS (all handler tests)

- [ ] **Step 5: Implement `ClientRepository` and its test**

```csharp
// tests/Catalog.Infrastructure.Tests/Persistence/Repositories/ClientRepositoryTests.cs
using AzureSuite.Catalog.Domain.Entities;
using AzureSuite.Catalog.Infrastructure.Persistence;
using AzureSuite.Catalog.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Catalog.Infrastructure.Tests.Persistence.Repositories
{
    public class ClientRepositoryTests
    {
        private static CatalogDbContext NewContext()
        {
            var options = new DbContextOptionsBuilder<CatalogDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new CatalogDbContext(options);
        }

        [Fact]
        public async Task AddAsync_ThenGetByIdAsync_ReturnsSameClient()
        {
            await using var context = NewContext();
            var repository = new ClientRepository(context);
            var client = new Client("Acme Corp");

            await repository.AddAsync(client, CancellationToken.None);
            var found = await repository.GetByIdAsync(client.Id, CancellationToken.None);

            found.Should().NotBeNull();
            found!.Name.Should().Be("Acme Corp");
        }

        [Fact]
        public async Task DeleteAsync_RemovesClient()
        {
            await using var context = NewContext();
            var repository = new ClientRepository(context);
            var client = new Client("Acme Corp");
            await repository.AddAsync(client, CancellationToken.None);

            await repository.DeleteAsync(client.Id, CancellationToken.None);

            (await repository.GetByIdAsync(client.Id, CancellationToken.None)).Should().BeNull();
        }
    }
}
```

```csharp
// services/Catalog/Catalog.Infrastructure/Persistence/Repositories/ClientRepository.cs
using AzureSuite.Catalog.Application.Abstractions;
using AzureSuite.Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AzureSuite.Catalog.Infrastructure.Persistence.Repositories
{
    public class ClientRepository : IClientRepository
    {
        private readonly CatalogDbContext _context;

        public ClientRepository(CatalogDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(Client client, CancellationToken cancellationToken)
        {
            _context.Clients.Add(client);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public Task<Client?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            return _context.Clients.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        }

        public async Task<IReadOnlyList<Client>> ListAsync(CancellationToken cancellationToken)
        {
            return await _context.Clients.ToListAsync(cancellationToken);
        }

        public async Task UpdateAsync(Client client, CancellationToken cancellationToken)
        {
            var existing = await _context.Clients.FirstAsync(c => c.Id == client.Id, cancellationToken);
            _context.Entry(existing).CurrentValues.SetValues(new { existing.Id, client.Name, existing.RegisteredAtUtc });
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
        {
            var existing = await _context.Clients.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
            if (existing is not null)
            {
                _context.Clients.Remove(existing);
                await _context.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
```

Note on `UpdateAsync`: `Client` has no setters, so the repository updates the tracked entity's shadow/current values directly via `CurrentValues.SetValues`, keeping the original `Id`/`RegisteredAtUtc` and only overwriting `Name` — this is the established EF Core pattern for updating immutable-looking entities without adding setters purely for persistence's sake.

- [ ] **Step 6: Run infrastructure tests**

Run: `dotnet test tests/Catalog.Infrastructure.Tests --filter ClientRepositoryTests`
Expected: PASS

- [ ] **Step 7: Wire endpoints into `Program.cs`**

```csharp
// services/Catalog/Catalog.Api/Contracts/CreateClientRequest.cs
namespace AzureSuite.Catalog.Api.Contracts
{
    public record CreateClientRequest(string Name);
}
```

```csharp
// services/Catalog/Catalog.Api/Contracts/UpdateClientRequest.cs
namespace AzureSuite.Catalog.Api.Contracts
{
    public record UpdateClientRequest(string Name);
}
```

```csharp
// Add to services/Catalog/Catalog.Api/Program.cs, alongside the message-types endpoints:
builder.Services.AddScoped<IClientRepository, ClientRepository>();
// ...
app.MapPost("/clients", async (CreateClientRequest request, IMediator mediator) =>
{
    var dto = await mediator.Send(new CreateClientCommand(request.Name));
    return Results.Created($"/clients/{dto.Id}", dto);
});

app.MapGet("/clients/{id:guid}", async (Guid id, IMediator mediator) =>
{
    var dto = await mediator.Send(new GetClientQuery(id));
    return dto is null ? Results.NotFound() : Results.Ok(dto);
});

app.MapGet("/clients", async (IMediator mediator) =>
{
    var dtos = await mediator.Send(new ListClientsQuery());
    return Results.Ok(dtos);
});

app.MapPut("/clients/{id:guid}", async (Guid id, UpdateClientRequest request, IMediator mediator) =>
{
    var dto = await mediator.Send(new UpdateClientCommand(id, request.Name));
    return Results.Ok(dto);
});

app.MapDelete("/clients/{id:guid}", async (Guid id, IMediator mediator) =>
{
    await mediator.Send(new DeleteClientCommand(id));
    return Results.NoContent();
});
```

Add the corresponding `using` statements for `IClientRepository`, `ClientRepository`, and each `Clients.Commands`/`Clients.Queries` namespace at the top of `Program.cs`, matching the existing `MessageTypes` `using` block.

- [ ] **Step 8: Run the full Catalog test suite**

Run: `dotnet test tests/Catalog.Application.Tests tests/Catalog.Infrastructure.Tests`
Expected: PASS, no warnings.

- [ ] **Step 9: Commit**

```bash
git add services/Catalog/Catalog.Application/Abstractions/IClientRepository.cs services/Catalog/Catalog.Application/Clients/ services/Catalog/Catalog.Infrastructure/Persistence/Repositories/ClientRepository.cs services/Catalog/Catalog.Api/Contracts/CreateClientRequest.cs services/Catalog/Catalog.Api/Contracts/UpdateClientRequest.cs services/Catalog/Catalog.Api/Program.cs tests/Catalog.Application.Tests/Clients/ tests/Catalog.Application.Tests/TestDoubles/FakeClientRepository.cs tests/Catalog.Infrastructure.Tests/Persistence/Repositories/ClientRepositoryTests.cs
git commit -m "feat(catalog): add Client CRUD (repository, handlers, endpoints)"
```

---

### Task 3: `Route` domain entity + persistence

**Files:**
- Create: `services/Catalog/Catalog.Domain/Entities/Route.cs`
- Modify: `services/Catalog/Catalog.Infrastructure/Persistence/CatalogDbContext.cs`
- Test: `tests/Catalog.Domain.Tests/Entities/RouteTests.cs`

**Interfaces:**
- Produces: `Route(Guid clientId, MessageTypeName messageTypeName, MessageTypeVersion messageTypeVersion, IEnumerable<string> queueNames)`; `Route.Id/ClientId/MessageTypeName/MessageTypeVersion/QueueNames`.

- [ ] **Step 1: Write the failing test**

```csharp
// tests/Catalog.Domain.Tests/Entities/RouteTests.cs
using AzureSuite.Catalog.Domain.Entities;
using AzureSuite.Catalog.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Catalog.Domain.Tests.Entities
{
    public class RouteTests
    {
        [Fact]
        public void Constructor_WithQueueNames_SetsProperties()
        {
            var clientId = Guid.NewGuid();
            var route = new Route(clientId, new MessageTypeName("pacs.008"), new MessageTypeVersion("1.0"), new[] { "queue-a", "queue-b" });

            route.Id.Should().NotBeEmpty();
            route.ClientId.Should().Be(clientId);
            route.MessageTypeName.Value.Should().Be("pacs.008");
            route.QueueNames.Should().Equal("queue-a", "queue-b");
        }

        [Fact]
        public void Constructor_WithNoQueueNames_ThrowsArgumentException()
        {
            var act = () => new Route(Guid.NewGuid(), new MessageTypeName("pacs.008"), new MessageTypeVersion("1.0"), Array.Empty<string>());

            act.Should().Throw<ArgumentException>();
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Catalog.Domain.Tests --filter RouteTests`
Expected: FAIL — `Route` does not exist.

- [ ] **Step 3: Write minimal implementation**

```csharp
// services/Catalog/Catalog.Domain/Entities/Route.cs
using AzureSuite.Catalog.Domain.ValueObjects;

namespace AzureSuite.Catalog.Domain.Entities
{
    /// <summary>Authorizes a Client to send a given MessageType/Version, and says where it goes.
    /// No Route for a (ClientId, MessageTypeName, MessageTypeVersion) triple means that client
    /// is not authorized to send that message type.</summary>
    public class Route
    {
        public Guid Id { get; }

        public Guid ClientId { get; }

        public MessageTypeName MessageTypeName { get; }

        public MessageTypeVersion MessageTypeVersion { get; }

        /// <summary>One or more Service Bus output queue names a matching message is fanned out to.</summary>
        public IReadOnlyList<string> QueueNames { get; }

        public Route(Guid clientId, MessageTypeName messageTypeName, MessageTypeVersion messageTypeVersion, IEnumerable<string> queueNames)
        {
            var names = queueNames.ToList();
            if (names.Count == 0)
            {
                throw new ArgumentException("At least one queue name is required.", nameof(queueNames));
            }

            Id = Guid.NewGuid();
            ClientId = clientId;
            MessageTypeName = messageTypeName;
            MessageTypeVersion = messageTypeVersion;
            QueueNames = names;
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/Catalog.Domain.Tests --filter RouteTests`
Expected: PASS

- [ ] **Step 5: Add `Route` to `CatalogDbContext`**

```csharp
// services/Catalog/Catalog.Infrastructure/Persistence/CatalogDbContext.cs
public DbSet<Route> Routes => Set<Route>();
```

```csharp
// Inside OnModelCreating:
modelBuilder.Entity<Route>(entity =>
{
    entity.HasKey(r => r.Id);
    entity.Property(r => r.Id).ValueGeneratedNever();
    entity.Property(r => r.ClientId).IsRequired();

    entity.Property(r => r.MessageTypeName)
        .HasConversion(name => name.Value, value => new MessageTypeName(value))
        .IsRequired()
        .HasMaxLength(100);

    entity.Property(r => r.MessageTypeVersion)
        .HasConversion(version => version.Value, value => new MessageTypeVersion(value))
        .IsRequired()
        .HasMaxLength(20);

    // Primitive collection of strings -> JSON column (EF Core 8+ default for relational
    // providers). Simpler than a normalized join table at this scale (1-3 queue names/route).
    entity.PrimitiveCollection(r => r.QueueNames).IsRequired();

    entity.HasIndex(r => new { r.ClientId, r.MessageTypeName, r.MessageTypeVersion });
});
```

- [ ] **Step 6: Add and apply the EF Core migration**

Run: `dotnet ef migrations add AddRoute --project services/Catalog/Catalog.Infrastructure --startup-project services/Catalog/Catalog.Api`
Expected: a new migration adding a `Routes` table with a `QueueNames` JSON column.

- [ ] **Step 7: Commit**

```bash
git add services/Catalog/Catalog.Domain/Entities/Route.cs services/Catalog/Catalog.Infrastructure/Persistence/CatalogDbContext.cs services/Catalog/Catalog.Infrastructure/Persistence/Migrations/ tests/Catalog.Domain.Tests/Entities/RouteTests.cs
git commit -m "feat(catalog): add Route entity"
```

---

### Task 4: `Route` repository, CQRS handlers, and API endpoints

**Files:**
- Create: `services/Catalog/Catalog.Application/Abstractions/IRouteRepository.cs`
- Create: `services/Catalog/Catalog.Application/Routes/RouteDto.cs`
- Create: `services/Catalog/Catalog.Application/Routes/Commands/CreateRoute/{CreateRouteCommand,CreateRouteHandler}.cs`
- Create: `services/Catalog/Catalog.Application/Routes/Commands/UpdateRoute/{UpdateRouteCommand,UpdateRouteHandler}.cs`
- Create: `services/Catalog/Catalog.Application/Routes/Commands/DeleteRoute/{DeleteRouteCommand,DeleteRouteHandler}.cs`
- Create: `services/Catalog/Catalog.Application/Routes/Queries/GetRoute/{GetRouteQuery,GetRouteHandler}.cs`
- Create: `services/Catalog/Catalog.Application/Routes/Queries/ListRoutes/{ListRoutesQuery,ListRoutesHandler}.cs`
- Create: `services/Catalog/Catalog.Infrastructure/Persistence/Repositories/RouteRepository.cs`
- Create: `services/Catalog/Catalog.Api/Contracts/{CreateRouteRequest,UpdateRouteRequest}.cs`
- Modify: `services/Catalog/Catalog.Api/Program.cs`
- Test: `tests/Catalog.Application.Tests/TestDoubles/FakeRouteRepository.cs`
- Test: `tests/Catalog.Application.Tests/Routes/Commands/CreateRoute/CreateRouteHandlerTests.cs`
- Test: `tests/Catalog.Application.Tests/Routes/Queries/GetRoute/GetRouteHandlerTests.cs`
- Test: `tests/Catalog.Application.Tests/Routes/Queries/ListRoutes/ListRoutesHandlerTests.cs`
- Test: `tests/Catalog.Infrastructure.Tests/Persistence/Repositories/RouteRepositoryTests.cs`

**Interfaces:**
- Consumes: `Route` from Task 3, `MessageTypeName`/`MessageTypeVersion` from `Catalog.Domain.ValueObjects`.
- Produces: `IRouteRepository` with `AddAsync`, `GetByIdAsync`, `ListAsync`, `UpdateAsync`, `DeleteAsync`, and `ListByClientAndTypeAsync(Guid clientId, MessageTypeName, MessageTypeVersion)` (consumed by Task 6's lookup query); `RouteDto(Guid Id, Guid ClientId, string MessageTypeName, string MessageTypeVersion, IReadOnlyList<string> QueueNames)`; endpoints `POST/GET/PUT/DELETE /routes[/{id}]`.

- [ ] **Step 1: Write the failing handler tests**

```csharp
// tests/Catalog.Application.Tests/TestDoubles/FakeRouteRepository.cs
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

        public Task UpdateAsync(Route route, CancellationToken cancellationToken)
        {
            var index = _routes.FindIndex(r => r.Id == route.Id);
            if (index >= 0) _routes[index] = route;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken)
        {
            _routes.RemoveAll(r => r.Id == id);
            return Task.CompletedTask;
        }
    }
}
```

```csharp
// tests/Catalog.Application.Tests/Routes/Commands/CreateRoute/CreateRouteHandlerTests.cs
using AzureSuite.Catalog.Application.Routes.Commands.CreateRoute;
using Catalog.Application.Tests.TestDoubles;
using FluentAssertions;
using Xunit;

namespace Catalog.Application.Tests.Routes.Commands.CreateRoute
{
    public class CreateRouteHandlerTests
    {
        [Fact]
        public async Task Handle_WithValidRequest_CreatesAndReturnsDto()
        {
            var repository = new FakeRouteRepository();
            var handler = new CreateRouteHandler(repository);
            var clientId = Guid.NewGuid();

            var result = await handler.Handle(
                new CreateRouteCommand(clientId, "pacs.008", "1.0", new[] { "queue-a" }),
                CancellationToken.None);

            result.ClientId.Should().Be(clientId);
            result.QueueNames.Should().Equal("queue-a");
        }
    }
}
```

```csharp
// tests/Catalog.Application.Tests/Routes/Queries/GetRoute/GetRouteHandlerTests.cs
using AzureSuite.Catalog.Application.Routes.Queries.GetRoute;
using AzureSuite.Catalog.Domain.Entities;
using AzureSuite.Catalog.Domain.ValueObjects;
using Catalog.Application.Tests.TestDoubles;
using FluentAssertions;
using Xunit;

namespace Catalog.Application.Tests.Routes.Queries.GetRoute
{
    public class GetRouteHandlerTests
    {
        [Fact]
        public async Task Handle_WithExistingId_ReturnsDto()
        {
            var repository = new FakeRouteRepository();
            var route = new Route(Guid.NewGuid(), new MessageTypeName("pacs.008"), new MessageTypeVersion("1.0"), new[] { "queue-a" });
            await repository.AddAsync(route, CancellationToken.None);
            var handler = new GetRouteHandler(repository);

            var result = await handler.Handle(new GetRouteQuery(route.Id), CancellationToken.None);

            result.Should().NotBeNull();
            result!.QueueNames.Should().Equal("queue-a");
        }
    }
}
```

```csharp
// tests/Catalog.Application.Tests/Routes/Queries/ListRoutes/ListRoutesHandlerTests.cs
using AzureSuite.Catalog.Application.Routes.Queries.ListRoutes;
using AzureSuite.Catalog.Domain.Entities;
using AzureSuite.Catalog.Domain.ValueObjects;
using Catalog.Application.Tests.TestDoubles;
using FluentAssertions;
using Xunit;

namespace Catalog.Application.Tests.Routes.Queries.ListRoutes
{
    public class ListRoutesHandlerTests
    {
        [Fact]
        public async Task Handle_ReturnsAllRoutes()
        {
            var repository = new FakeRouteRepository();
            await repository.AddAsync(new Route(Guid.NewGuid(), new MessageTypeName("pacs.008"), new MessageTypeVersion("1.0"), new[] { "queue-a" }), CancellationToken.None);
            await repository.AddAsync(new Route(Guid.NewGuid(), new MessageTypeName("camt.054"), new MessageTypeVersion("1.0"), new[] { "queue-b", "queue-c" }), CancellationToken.None);
            var handler = new ListRoutesHandler(repository);

            var result = await handler.Handle(new ListRoutesQuery(), CancellationToken.None);

            result.Should().HaveCount(2);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Catalog.Application.Tests --filter FullyQualifiedName~Routes`
Expected: FAIL

- [ ] **Step 3: Write the abstraction, DTO, and handlers**

```csharp
// services/Catalog/Catalog.Application/Abstractions/IRouteRepository.cs
using AzureSuite.Catalog.Domain.Entities;
using AzureSuite.Catalog.Domain.ValueObjects;

namespace AzureSuite.Catalog.Application.Abstractions
{
    public interface IRouteRepository
    {
        Task AddAsync(Route route, CancellationToken cancellationToken);
        Task<Route?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
        Task<IReadOnlyList<Route>> ListAsync(CancellationToken cancellationToken);

        /// <summary>Used by the route-lookup query (Task 6): every Route authorizing this
        /// client to send this message type/version.</summary>
        Task<IReadOnlyList<Route>> ListByClientAndTypeAsync(Guid clientId, MessageTypeName messageTypeName, MessageTypeVersion messageTypeVersion, CancellationToken cancellationToken);

        Task UpdateAsync(Route route, CancellationToken cancellationToken);
        Task DeleteAsync(Guid id, CancellationToken cancellationToken);
    }
}
```

```csharp
// services/Catalog/Catalog.Application/Routes/RouteDto.cs
namespace AzureSuite.Catalog.Application.Routes
{
    public record RouteDto(Guid Id, Guid ClientId, string MessageTypeName, string MessageTypeVersion, IReadOnlyList<string> QueueNames);
}
```

```csharp
// services/Catalog/Catalog.Application/Routes/Commands/CreateRoute/CreateRouteCommand.cs
using MediatR;

namespace AzureSuite.Catalog.Application.Routes.Commands.CreateRoute
{
    public record CreateRouteCommand(Guid ClientId, string MessageTypeName, string MessageTypeVersion, IReadOnlyList<string> QueueNames) : IRequest<RouteDto>;
}
```

```csharp
// services/Catalog/Catalog.Application/Routes/Commands/CreateRoute/CreateRouteHandler.cs
using AzureSuite.Catalog.Application.Abstractions;
using AzureSuite.Catalog.Domain.Entities;
using AzureSuite.Catalog.Domain.ValueObjects;
using MediatR;

namespace AzureSuite.Catalog.Application.Routes.Commands.CreateRoute
{
    public class CreateRouteHandler : IRequestHandler<CreateRouteCommand, RouteDto>
    {
        private readonly IRouteRepository _repository;

        public CreateRouteHandler(IRouteRepository repository)
        {
            _repository = repository;
        }

        public async Task<RouteDto> Handle(CreateRouteCommand request, CancellationToken cancellationToken)
        {
            var route = new Route(
                request.ClientId,
                new MessageTypeName(request.MessageTypeName),
                new MessageTypeVersion(request.MessageTypeVersion),
                request.QueueNames);

            await _repository.AddAsync(route, cancellationToken);

            return new RouteDto(route.Id, route.ClientId, route.MessageTypeName.Value, route.MessageTypeVersion.Value, route.QueueNames);
        }
    }
}
```

```csharp
// services/Catalog/Catalog.Application/Routes/Queries/GetRoute/GetRouteQuery.cs
using MediatR;

namespace AzureSuite.Catalog.Application.Routes.Queries.GetRoute
{
    public record GetRouteQuery(Guid Id) : IRequest<RouteDto?>;
}
```

```csharp
// services/Catalog/Catalog.Application/Routes/Queries/GetRoute/GetRouteHandler.cs
using AzureSuite.Catalog.Application.Abstractions;
using MediatR;

namespace AzureSuite.Catalog.Application.Routes.Queries.GetRoute
{
    public class GetRouteHandler : IRequestHandler<GetRouteQuery, RouteDto?>
    {
        private readonly IRouteRepository _repository;

        public GetRouteHandler(IRouteRepository repository)
        {
            _repository = repository;
        }

        public async Task<RouteDto?> Handle(GetRouteQuery request, CancellationToken cancellationToken)
        {
            var route = await _repository.GetByIdAsync(request.Id, cancellationToken);
            return route is null ? null : new RouteDto(route.Id, route.ClientId, route.MessageTypeName.Value, route.MessageTypeVersion.Value, route.QueueNames);
        }
    }
}
```

```csharp
// services/Catalog/Catalog.Application/Routes/Queries/ListRoutes/ListRoutesQuery.cs
using MediatR;

namespace AzureSuite.Catalog.Application.Routes.Queries.ListRoutes
{
    public record ListRoutesQuery : IRequest<IReadOnlyList<RouteDto>>;
}
```

```csharp
// services/Catalog/Catalog.Application/Routes/Queries/ListRoutes/ListRoutesHandler.cs
using AzureSuite.Catalog.Application.Abstractions;
using MediatR;

namespace AzureSuite.Catalog.Application.Routes.Queries.ListRoutes
{
    public class ListRoutesHandler : IRequestHandler<ListRoutesQuery, IReadOnlyList<RouteDto>>
    {
        private readonly IRouteRepository _repository;

        public ListRoutesHandler(IRouteRepository repository)
        {
            _repository = repository;
        }

        public async Task<IReadOnlyList<RouteDto>> Handle(ListRoutesQuery request, CancellationToken cancellationToken)
        {
            var routes = await _repository.ListAsync(cancellationToken);
            return routes.Select(r => new RouteDto(r.Id, r.ClientId, r.MessageTypeName.Value, r.MessageTypeVersion.Value, r.QueueNames)).ToList();
        }
    }
}
```

```csharp
// services/Catalog/Catalog.Application/Routes/Commands/UpdateRoute/UpdateRouteCommand.cs
using MediatR;

namespace AzureSuite.Catalog.Application.Routes.Commands.UpdateRoute
{
    public record UpdateRouteCommand(Guid Id, Guid ClientId, string MessageTypeName, string MessageTypeVersion, IReadOnlyList<string> QueueNames) : IRequest<RouteDto>;
}
```

```csharp
// services/Catalog/Catalog.Application/Routes/Commands/UpdateRoute/UpdateRouteHandler.cs
using AzureSuite.Catalog.Application.Abstractions;
using AzureSuite.Catalog.Domain.Entities;
using AzureSuite.Catalog.Domain.ValueObjects;
using MediatR;

namespace AzureSuite.Catalog.Application.Routes.Commands.UpdateRoute
{
    public class UpdateRouteHandler : IRequestHandler<UpdateRouteCommand, RouteDto>
    {
        private readonly IRouteRepository _repository;

        public UpdateRouteHandler(IRouteRepository repository)
        {
            _repository = repository;
        }

        public async Task<RouteDto> Handle(UpdateRouteCommand request, CancellationToken cancellationToken)
        {
            var existing = await _repository.GetByIdAsync(request.Id, cancellationToken);
            if (existing is null)
            {
                throw new InvalidOperationException($"Route '{request.Id}' was not found.");
            }

            var updated = new Route(
                request.ClientId,
                new MessageTypeName(request.MessageTypeName),
                new MessageTypeVersion(request.MessageTypeVersion),
                request.QueueNames);

            await _repository.UpdateAsync(updated, cancellationToken);

            return new RouteDto(existing.Id, updated.ClientId, updated.MessageTypeName.Value, updated.MessageTypeVersion.Value, updated.QueueNames);
        }
    }
}
```

```csharp
// services/Catalog/Catalog.Application/Routes/Commands/DeleteRoute/DeleteRouteCommand.cs
using MediatR;

namespace AzureSuite.Catalog.Application.Routes.Commands.DeleteRoute
{
    public record DeleteRouteCommand(Guid Id) : IRequest;
}
```

```csharp
// services/Catalog/Catalog.Application/Routes/Commands/DeleteRoute/DeleteRouteHandler.cs
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
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Catalog.Application.Tests --filter FullyQualifiedName~Routes`
Expected: PASS

- [ ] **Step 5: Implement `RouteRepository` and its test**

```csharp
// tests/Catalog.Infrastructure.Tests/Persistence/Repositories/RouteRepositoryTests.cs
using AzureSuite.Catalog.Domain.Entities;
using AzureSuite.Catalog.Domain.ValueObjects;
using AzureSuite.Catalog.Infrastructure.Persistence;
using AzureSuite.Catalog.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Catalog.Infrastructure.Tests.Persistence.Repositories
{
    public class RouteRepositoryTests
    {
        private static CatalogDbContext NewContext()
        {
            var options = new DbContextOptionsBuilder<CatalogDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new CatalogDbContext(options);
        }

        [Fact]
        public async Task AddAsync_ThenGetByIdAsync_PreservesQueueNames()
        {
            await using var context = NewContext();
            var repository = new RouteRepository(context);
            var route = new Route(Guid.NewGuid(), new MessageTypeName("pacs.008"), new MessageTypeVersion("1.0"), new[] { "queue-a", "queue-b" });

            await repository.AddAsync(route, CancellationToken.None);
            var found = await repository.GetByIdAsync(route.Id, CancellationToken.None);

            found!.QueueNames.Should().Equal("queue-a", "queue-b");
        }

        [Fact]
        public async Task ListByClientAndTypeAsync_ReturnsOnlyMatchingRoutes()
        {
            await using var context = NewContext();
            var repository = new RouteRepository(context);
            var clientId = Guid.NewGuid();
            await repository.AddAsync(new Route(clientId, new MessageTypeName("pacs.008"), new MessageTypeVersion("1.0"), new[] { "queue-a" }), CancellationToken.None);
            await repository.AddAsync(new Route(Guid.NewGuid(), new MessageTypeName("pacs.008"), new MessageTypeVersion("1.0"), new[] { "queue-b" }), CancellationToken.None);

            var result = await repository.ListByClientAndTypeAsync(clientId, new MessageTypeName("pacs.008"), new MessageTypeVersion("1.0"), CancellationToken.None);

            result.Should().ContainSingle().Which.QueueNames.Should().Equal("queue-a");
        }
    }
}
```

```csharp
// services/Catalog/Catalog.Infrastructure/Persistence/Repositories/RouteRepository.cs
using AzureSuite.Catalog.Application.Abstractions;
using AzureSuite.Catalog.Domain.Entities;
using AzureSuite.Catalog.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace AzureSuite.Catalog.Infrastructure.Persistence.Repositories
{
    public class RouteRepository : IRouteRepository
    {
        private readonly CatalogDbContext _context;

        public RouteRepository(CatalogDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(Route route, CancellationToken cancellationToken)
        {
            _context.Routes.Add(route);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public Task<Route?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            return _context.Routes.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        }

        public async Task<IReadOnlyList<Route>> ListAsync(CancellationToken cancellationToken)
        {
            return await _context.Routes.ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<Route>> ListByClientAndTypeAsync(Guid clientId, MessageTypeName messageTypeName, MessageTypeVersion messageTypeVersion, CancellationToken cancellationToken)
        {
            return await _context.Routes
                .Where(r => r.ClientId == clientId && r.MessageTypeName == messageTypeName && r.MessageTypeVersion == messageTypeVersion)
                .ToListAsync(cancellationToken);
        }

        public async Task UpdateAsync(Route route, CancellationToken cancellationToken)
        {
            var existing = await _context.Routes.FirstAsync(r => r.Id == route.Id, cancellationToken);
            _context.Entry(existing).CurrentValues.SetValues(new
            {
                existing.Id,
                route.ClientId,
                MessageTypeName = route.MessageTypeName.Value,
                MessageTypeVersion = route.MessageTypeVersion.Value
            });
            existing.GetType().GetProperty(nameof(Route.QueueNames))!.SetValue(existing, route.QueueNames);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
        {
            var existing = await _context.Routes.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
            if (existing is not null)
            {
                _context.Routes.Remove(existing);
                await _context.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
```

Note: `CurrentValues.SetValues` can't target the `QueueNames` primitive collection through the anonymous-object overload, so it's set via reflection as a pragmatic exception to the "no setters" rule's spirit — the property itself still has no public setter. If this proves awkward when actually implementing, an acceptable alternative is deleting and re-adding the `Route` row in `UpdateAsync` (same net effect, simpler code); either is fine, this is an implementation detail the spec doesn't pin down.

- [ ] **Step 6: Run infrastructure tests**

Run: `dotnet test tests/Catalog.Infrastructure.Tests --filter RouteRepositoryTests`
Expected: PASS

- [ ] **Step 7: Wire endpoints into `Program.cs`**

```csharp
// services/Catalog/Catalog.Api/Contracts/CreateRouteRequest.cs
namespace AzureSuite.Catalog.Api.Contracts
{
    public record CreateRouteRequest(Guid ClientId, string MessageTypeName, string MessageTypeVersion, IReadOnlyList<string> QueueNames);
}
```

```csharp
// services/Catalog/Catalog.Api/Contracts/UpdateRouteRequest.cs
namespace AzureSuite.Catalog.Api.Contracts
{
    public record UpdateRouteRequest(Guid ClientId, string MessageTypeName, string MessageTypeVersion, IReadOnlyList<string> QueueNames);
}
```

```csharp
// Add to services/Catalog/Catalog.Api/Program.cs:
builder.Services.AddScoped<IRouteRepository, RouteRepository>();
// ...
app.MapPost("/routes", async (CreateRouteRequest request, IMediator mediator) =>
{
    var dto = await mediator.Send(new CreateRouteCommand(request.ClientId, request.MessageTypeName, request.MessageTypeVersion, request.QueueNames));
    return Results.Created($"/routes/{dto.Id}", dto);
});

app.MapGet("/routes/{id:guid}", async (Guid id, IMediator mediator) =>
{
    var dto = await mediator.Send(new GetRouteQuery(id));
    return dto is null ? Results.NotFound() : Results.Ok(dto);
});

app.MapGet("/routes", async (IMediator mediator) =>
{
    var dtos = await mediator.Send(new ListRoutesQuery());
    return Results.Ok(dtos);
});

app.MapPut("/routes/{id:guid}", async (Guid id, UpdateRouteRequest request, IMediator mediator) =>
{
    var dto = await mediator.Send(new UpdateRouteCommand(id, request.ClientId, request.MessageTypeName, request.MessageTypeVersion, request.QueueNames));
    return Results.Ok(dto);
});

app.MapDelete("/routes/{id:guid}", async (Guid id, IMediator mediator) =>
{
    await mediator.Send(new DeleteRouteCommand(id));
    return Results.NoContent();
});
```

Note: this task maps `/routes/{id:guid}` as a route-constrained pattern specifically so it never matches the literal `/routes/lookup` path Task 6 adds — ASP.NET Core's routing picks the more specific literal segment (`lookup`) over the `{id:guid}` parameter automatically, so no explicit route ordering is needed, but Task 6 must add its endpoint using the exact literal segment `/routes/lookup`, not a parameterized path.

- [ ] **Step 8: Run the full Catalog test suite**

Run: `dotnet test tests/Catalog.Application.Tests tests/Catalog.Infrastructure.Tests`
Expected: PASS, no warnings.

- [ ] **Step 9: Commit**

```bash
git add services/Catalog/Catalog.Application/Abstractions/IRouteRepository.cs services/Catalog/Catalog.Application/Routes/ services/Catalog/Catalog.Infrastructure/Persistence/Repositories/RouteRepository.cs services/Catalog/Catalog.Api/Contracts/CreateRouteRequest.cs services/Catalog/Catalog.Api/Contracts/UpdateRouteRequest.cs services/Catalog/Catalog.Api/Program.cs tests/Catalog.Application.Tests/Routes/ tests/Catalog.Application.Tests/TestDoubles/FakeRouteRepository.cs tests/Catalog.Infrastructure.Tests/Persistence/Repositories/RouteRepositoryTests.cs
git commit -m "feat(catalog): add Route CRUD (repository, handlers, endpoints)"
```

---

### Task 5: Schema-validation endpoint

**Files:**
- Modify: `services/Catalog/Catalog.Api/Catalog.Api.csproj` (add `JsonSchema.Net` package)
- Create: `services/Catalog/Catalog.Application/MessageTypes/Queries/ValidateMessage/{ValidateMessageQuery,ValidateMessageHandler,ValidationResultDto}.cs`
- Create: `services/Catalog/Catalog.Api/Contracts/ValidateMessageRequest.cs`
- Modify: `services/Catalog/Catalog.Api/Program.cs`
- Test: `tests/Catalog.Application.Tests/MessageTypes/Queries/ValidateMessage/ValidateMessageHandlerTests.cs`

**Interfaces:**
- Consumes: `IMessageTypeRepository.GetByNameAndVersionAsync` (existing).
- Produces: `ValidationResultDto(bool IsValid, IReadOnlyList<string> Errors)`; endpoint `POST /message-types/{name}/{version}/validate`.

- [ ] **Step 1: Add the `JsonSchema.Net` package**

```bash
dotnet add services/Catalog/Catalog.Application package JsonSchema.Net
```

- [ ] **Step 2: Write the failing handler test**

```csharp
// tests/Catalog.Application.Tests/MessageTypes/Queries/ValidateMessage/ValidateMessageHandlerTests.cs
using AzureSuite.Catalog.Application.MessageTypes.Queries.ValidateMessage;
using AzureSuite.Catalog.Domain.Entities;
using AzureSuite.Catalog.Domain.ValueObjects;
using Catalog.Application.Tests.TestDoubles;
using FluentAssertions;
using Xunit;

namespace Catalog.Application.Tests.MessageTypes.Queries.ValidateMessage
{
    public class ValidateMessageHandlerTests
    {
        private const string Schema = """
        {
          "type": "object",
          "properties": { "amount": { "type": "number" } },
          "required": ["amount"]
        }
        """;

        [Fact]
        public async Task Handle_WithConformingPayload_ReturnsValid()
        {
            var repository = new FakeMessageTypeRepository();
            await repository.AddAsync(new MessageType(new MessageTypeName("pacs.008"), new MessageTypeVersion("1.0"), Schema), CancellationToken.None);
            var handler = new ValidateMessageHandler(repository);

            var result = await handler.Handle(new ValidateMessageQuery("pacs.008", "1.0", """{ "amount": 100 }"""), CancellationToken.None);

            result.IsValid.Should().BeTrue();
            result.Errors.Should().BeEmpty();
        }

        [Fact]
        public async Task Handle_WithNonConformingPayload_ReturnsInvalidWithErrors()
        {
            var repository = new FakeMessageTypeRepository();
            await repository.AddAsync(new MessageType(new MessageTypeName("pacs.008"), new MessageTypeVersion("1.0"), Schema), CancellationToken.None);
            var handler = new ValidateMessageHandler(repository);

            var result = await handler.Handle(new ValidateMessageQuery("pacs.008", "1.0", """{ "amount": "not-a-number" }"""), CancellationToken.None);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().NotBeEmpty();
        }

        [Fact]
        public async Task Handle_WithUnknownMessageType_ReturnsInvalidWithNotRegisteredError()
        {
            var handler = new ValidateMessageHandler(new FakeMessageTypeRepository());

            var result = await handler.Handle(new ValidateMessageQuery("unknown", "1.0", "{}"), CancellationToken.None);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.Contains("not registered"));
        }
    }
}
```

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test tests/Catalog.Application.Tests --filter ValidateMessageHandlerTests`
Expected: FAIL

- [ ] **Step 4: Write minimal implementation**

```csharp
// services/Catalog/Catalog.Application/MessageTypes/Queries/ValidateMessage/ValidationResultDto.cs
namespace AzureSuite.Catalog.Application.MessageTypes.Queries.ValidateMessage
{
    public record ValidationResultDto(bool IsValid, IReadOnlyList<string> Errors);
}
```

```csharp
// services/Catalog/Catalog.Application/MessageTypes/Queries/ValidateMessage/ValidateMessageQuery.cs
using MediatR;

namespace AzureSuite.Catalog.Application.MessageTypes.Queries.ValidateMessage
{
    /// <summary>Checks only whether Payload is structurally valid for the given MessageType/
    /// Version's registered schema. Knows nothing about clients or routing.</summary>
    public record ValidateMessageQuery(string MessageTypeName, string MessageTypeVersion, string Payload) : IRequest<ValidationResultDto>;
}
```

```csharp
// services/Catalog/Catalog.Application/MessageTypes/Queries/ValidateMessage/ValidateMessageHandler.cs
using AzureSuite.Catalog.Application.Abstractions;
using AzureSuite.Catalog.Domain.ValueObjects;
using Json.Schema;
using MediatR;
using System.Text.Json;

namespace AzureSuite.Catalog.Application.MessageTypes.Queries.ValidateMessage
{
    public class ValidateMessageHandler : IRequestHandler<ValidateMessageQuery, ValidationResultDto>
    {
        private readonly IMessageTypeRepository _repository;

        public ValidateMessageHandler(IMessageTypeRepository repository)
        {
            _repository = repository;
        }

        public async Task<ValidationResultDto> Handle(ValidateMessageQuery request, CancellationToken cancellationToken)
        {
            var messageType = await _repository.GetByNameAndVersionAsync(
                new MessageTypeName(request.MessageTypeName),
                new MessageTypeVersion(request.MessageTypeVersion),
                cancellationToken);

            if (messageType is null)
            {
                return new ValidationResultDto(false, new[] { $"Message type '{request.MessageTypeName}' version '{request.MessageTypeVersion}' is not registered." });
            }

            var schema = JsonSchema.FromText(messageType.SchemaDefinition);
            using var payloadDocument = JsonDocument.Parse(request.Payload);
            var evaluationResult = schema.Evaluate(payloadDocument.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List });

            if (evaluationResult.IsValid)
            {
                return new ValidationResultDto(true, Array.Empty<string>());
            }

            var errors = evaluationResult.Details
                .Where(d => !d.IsValid && d.Errors is not null)
                .SelectMany(d => d.Errors!.Values)
                .ToList();

            return new ValidationResultDto(false, errors);
        }
    }
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test tests/Catalog.Application.Tests --filter ValidateMessageHandlerTests`
Expected: PASS

- [ ] **Step 6: Wire the endpoint into `Program.cs`**

```csharp
// services/Catalog/Catalog.Api/Contracts/ValidateMessageRequest.cs
namespace AzureSuite.Catalog.Api.Contracts
{
    public record ValidateMessageRequest(string Payload);
}
```

```csharp
// Add to services/Catalog/Catalog.Api/Program.cs:
app.MapPost("/message-types/{name}/{version}/validate", async (string name, string version, ValidateMessageRequest request, IMediator mediator) =>
{
    var result = await mediator.Send(new ValidateMessageQuery(name, version, request.Payload));
    return Results.Ok(result);
});
```

- [ ] **Step 7: Run the full Catalog test suite**

Run: `dotnet test tests/Catalog.Application.Tests`
Expected: PASS, no warnings.

- [ ] **Step 8: Commit**

```bash
git add services/Catalog/Catalog.Application/MessageTypes/Queries/ValidateMessage/ services/Catalog/Catalog.Api/Contracts/ValidateMessageRequest.cs services/Catalog/Catalog.Api/Program.cs services/Catalog/Catalog.Application/Catalog.Application.csproj tests/Catalog.Application.Tests/MessageTypes/Queries/ValidateMessage/
git commit -m "feat(catalog): add schema-validation endpoint"
```

---

### Task 6: Route-lookup endpoint

**Files:**
- Create: `services/Catalog/Catalog.Application/Routes/Queries/LookupRoute/{LookupRouteQuery,LookupRouteHandler}.cs`
- Modify: `services/Catalog/Catalog.Api/Program.cs`
- Test: `tests/Catalog.Application.Tests/Routes/Queries/LookupRoute/LookupRouteHandlerTests.cs`

**Interfaces:**
- Consumes: `IRouteRepository.ListByClientAndTypeAsync` from Task 4.
- Produces: endpoint `GET /routes/lookup?clientId={id}&messageType={name}&version={v}` → `{ queueNames: string[] }` (empty array = not authorized).

- [ ] **Step 1: Write the failing handler test**

```csharp
// tests/Catalog.Application.Tests/Routes/Queries/LookupRoute/LookupRouteHandlerTests.cs
using AzureSuite.Catalog.Application.Routes.Queries.LookupRoute;
using AzureSuite.Catalog.Domain.Entities;
using AzureSuite.Catalog.Domain.ValueObjects;
using Catalog.Application.Tests.TestDoubles;
using FluentAssertions;
using Xunit;

namespace Catalog.Application.Tests.Routes.Queries.LookupRoute
{
    public class LookupRouteHandlerTests
    {
        [Fact]
        public async Task Handle_WithAuthorizedClientAndType_ReturnsQueueNames()
        {
            var repository = new FakeRouteRepository();
            var clientId = Guid.NewGuid();
            await repository.AddAsync(new Route(clientId, new MessageTypeName("pacs.008"), new MessageTypeVersion("1.0"), new[] { "queue-a", "queue-b" }), CancellationToken.None);
            var handler = new LookupRouteHandler(repository);

            var result = await handler.Handle(new LookupRouteQuery(clientId, "pacs.008", "1.0"), CancellationToken.None);

            result.QueueNames.Should().Equal("queue-a", "queue-b");
        }

        [Fact]
        public async Task Handle_WithNoMatchingRoute_ReturnsEmptyQueueNames()
        {
            var handler = new LookupRouteHandler(new FakeRouteRepository());

            var result = await handler.Handle(new LookupRouteQuery(Guid.NewGuid(), "pacs.008", "1.0"), CancellationToken.None);

            result.QueueNames.Should().BeEmpty();
        }

        [Fact]
        public async Task Handle_WithMultipleRoutesForSamePair_MergesAllQueueNames()
        {
            var repository = new FakeRouteRepository();
            var clientId = Guid.NewGuid();
            await repository.AddAsync(new Route(clientId, new MessageTypeName("pacs.008"), new MessageTypeVersion("1.0"), new[] { "queue-a" }), CancellationToken.None);
            await repository.AddAsync(new Route(clientId, new MessageTypeName("pacs.008"), new MessageTypeVersion("1.0"), new[] { "queue-b" }), CancellationToken.None);
            var handler = new LookupRouteHandler(repository);

            var result = await handler.Handle(new LookupRouteQuery(clientId, "pacs.008", "1.0"), CancellationToken.None);

            result.QueueNames.Should().BeEquivalentTo(new[] { "queue-a", "queue-b" });
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Catalog.Application.Tests --filter LookupRouteHandlerTests`
Expected: FAIL

- [ ] **Step 3: Write minimal implementation**

```csharp
// services/Catalog/Catalog.Application/Routes/Queries/LookupRoute/LookupRouteQuery.cs
using MediatR;

namespace AzureSuite.Catalog.Application.Routes.Queries.LookupRoute
{
    /// <summary>Checks only whether ClientId is authorized to send MessageTypeName/Version, and
    /// if so, which queues it's routed to. Knows nothing about payload shape or schemas.</summary>
    public record LookupRouteQuery(Guid ClientId, string MessageTypeName, string MessageTypeVersion) : IRequest<LookupRouteResultDto>;

    public record LookupRouteResultDto(IReadOnlyList<string> QueueNames);
}
```

```csharp
// services/Catalog/Catalog.Application/Routes/Queries/LookupRoute/LookupRouteHandler.cs
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
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/Catalog.Application.Tests --filter LookupRouteHandlerTests`
Expected: PASS

- [ ] **Step 5: Wire the endpoint into `Program.cs`**

```csharp
// Add to services/Catalog/Catalog.Api/Program.cs — note the literal "lookup" segment,
// which ASP.NET Core routing matches ahead of the "/routes/{id:guid}" pattern from Task 4:
app.MapGet("/routes/lookup", async (Guid clientId, string messageType, string version, IMediator mediator) =>
{
    var result = await mediator.Send(new LookupRouteQuery(clientId, messageType, version));
    return Results.Ok(result);
});
```

- [ ] **Step 6: Run the full Catalog test suite**

Run: `dotnet test tests/Catalog.Application.Tests tests/Catalog.Infrastructure.Tests`
Expected: PASS, no warnings.

- [ ] **Step 7: Commit**

```bash
git add services/Catalog/Catalog.Application/Routes/Queries/LookupRoute/ services/Catalog/Catalog.Api/Program.cs tests/Catalog.Application.Tests/Routes/Queries/LookupRoute/
git commit -m "feat(catalog): add route-lookup endpoint"
```

---

### Task 7: Blazor UI — Clients management

**Files:**
- Create: `frontends/AzureSuite.Web.Blazor/Features/Catalog/Contracts/{ClientDto,CreateClientRequest,UpdateClientRequest}.cs`
- Modify: `frontends/AzureSuite.Web.Blazor/Features/Catalog/Services/CatalogApiClient.cs`
- Create: `frontends/AzureSuite.Web.Blazor/Features/Catalog/Pages/ClientsList.razor` + `.razor.cs`
- Create: `frontends/AzureSuite.Web.Blazor/Features/Catalog/Pages/RegisterClient.razor` + `.razor.cs`
- Modify: `frontends/AzureSuite.Web.Blazor/Layout/NavMenu.razor`
- Test: `tests/AzureSuite.Web.Blazor.Tests/Features/Catalog/Pages/ClientsListTests.cs`

**Interfaces:**
- Consumes: `Catalog.Api`'s `/clients` endpoints from Task 2.
- Produces: `CatalogApiClient.GetClientsAsync`, `.CreateClientAsync`; route `/catalog/clients`, `/catalog/clients/register`.

- [ ] **Step 1: Add web-side contracts**

```csharp
// frontends/AzureSuite.Web.Blazor/Features/Catalog/Contracts/ClientDto.cs
namespace AzureSuite.Web.Blazor.Features.Catalog.Contracts
{
    public record ClientDto(Guid Id, string Name, DateTime RegisteredAtUtc);
}
```

```csharp
// frontends/AzureSuite.Web.Blazor/Features/Catalog/Contracts/CreateClientRequest.cs
namespace AzureSuite.Web.Blazor.Features.Catalog.Contracts
{
    public record CreateClientRequest(string Name);
}
```

- [ ] **Step 2: Write the failing page test**

```csharp
// tests/AzureSuite.Web.Blazor.Tests/Features/Catalog/Pages/ClientsListTests.cs
using AzureSuite.Web.Blazor.Features.Catalog.Pages;
using AzureSuite.Web.Blazor.Features.Catalog.Services;
using Bunit;
using FluentAssertions;
using Xunit;

namespace AzureSuite.Web.Blazor.Tests.Features.Catalog.Pages
{
    public class ClientsListTests : BunitContext
    {
        public ClientsListTests()
        {
            Services.AddScoped(_ => new CatalogApiClient(new HttpClient { BaseAddress = new Uri("https://localhost/") }));
        }

        [Fact]
        public void RendersHeading()
        {
            var cut = Render<ClientsList>();

            cut.Find("h1").TextContent.Should().Be("Clients");
        }
    }
}
```

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test tests/AzureSuite.Web.Blazor.Tests --filter ClientsListTests`
Expected: FAIL — `ClientsList` does not exist.

- [ ] **Step 4: Add `CatalogApiClient` methods**

```csharp
// Add to frontends/AzureSuite.Web.Blazor/Features/Catalog/Services/CatalogApiClient.cs
public async Task<IReadOnlyList<ClientDto>> GetClientsAsync(CancellationToken cancellationToken)
{
    var result = await _httpClient.GetFromJsonAsync<List<ClientDto>>("/clients", cancellationToken);
    return result ?? new List<ClientDto>();
}

public async Task<ClientDto> CreateClientAsync(CreateClientRequest request, CancellationToken cancellationToken)
{
    var response = await _httpClient.PostAsJsonAsync("/clients", request, cancellationToken);
    response.EnsureSuccessStatusCode();
    return (await response.Content.ReadFromJsonAsync<ClientDto>(cancellationToken))!;
}
```

Add `using AzureSuite.Web.Blazor.Features.Catalog.Contracts;` at the top if not already present (it already is, per the existing `RegisterMessageTypeRequest` usage).

- [ ] **Step 5: Implement `ClientsList` page**

```razor
@* frontends/AzureSuite.Web.Blazor/Features/Catalog/Pages/ClientsList.razor *@
@page "/catalog/clients"

<PageHeader Title="Clients" Eyebrow="Catalog">
    <Actions>
        <AppButton Href="/catalog/clients/register">Register client</AppButton>
    </Actions>
</PageHeader>

@if (Clients is null)
{
    <p>Loading...</p>
}
else if (Clients.Count == 0)
{
    <p class="subtitle">No clients registered yet.</p>
}
else
{
    <table class="data-table">
        <thead>
            <tr><th>Name</th><th>Registered</th></tr>
        </thead>
        <tbody>
            @foreach (var client in Clients)
            {
                <tr>
                    <td>@client.Name</td>
                    <td>@client.RegisteredAtUtc.ToString("u")</td>
                </tr>
            }
        </tbody>
    </table>
}
```

```csharp
// frontends/AzureSuite.Web.Blazor/Features/Catalog/Pages/ClientsList.razor.cs
using AzureSuite.Web.Blazor.Features.Catalog.Contracts;
using AzureSuite.Web.Blazor.Features.Catalog.Services;
using Microsoft.AspNetCore.Components;

namespace AzureSuite.Web.Blazor.Features.Catalog.Pages
{
    public partial class ClientsList : ComponentBase
    {
        [Inject] public CatalogApiClient ApiClient { get; set; } = default!;

        private IReadOnlyList<ClientDto>? Clients;

        protected override async Task OnInitializedAsync()
        {
            Clients = await ApiClient.GetClientsAsync(CancellationToken.None);
        }
    }
}
```

- [ ] **Step 6: Run test to verify it passes**

Run: `dotnet test tests/AzureSuite.Web.Blazor.Tests --filter ClientsListTests`
Expected: PASS

- [ ] **Step 7: Implement `RegisterClient` page**

```razor
@* frontends/AzureSuite.Web.Blazor/Features/Catalog/Pages/RegisterClient.razor *@
@page "/catalog/clients/register"

<PageHeader Title="Register Client" Eyebrow="Catalog">
    <Actions>
        <AppButton Href="/catalog/clients" Variant="AppButtonVariant.Secondary">Back to list</AppButton>
    </Actions>
</PageHeader>

<EditForm EditContext="@EditContext" OnValidSubmit="@SubmitAsync">
    <div class="form-group">
        <label>Name</label>
        <InputText @bind-Value="Request.Name" />
    </div>
    <AppButton Type="submit">Register</AppButton>
</EditForm>

@if (ErrorMessage is not null)
{
    <ErrorState Message="@ErrorMessage" />
}
```

```csharp
// frontends/AzureSuite.Web.Blazor/Features/Catalog/Pages/RegisterClient.razor.cs
using AzureSuite.Web.Blazor.Features.Catalog.Contracts;
using AzureSuite.Web.Blazor.Features.Catalog.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace AzureSuite.Web.Blazor.Features.Catalog.Pages
{
    public partial class RegisterClient : ComponentBase
    {
        [Inject] public CatalogApiClient ApiClient { get; set; } = default!;
        [Inject] public NavigationManager Navigation { get; set; } = default!;

        private CreateClientRequest Request = new("");
        private EditContext EditContext = default!;
        private string? ErrorMessage;

        protected override void OnInitialized()
        {
            EditContext = new EditContext(Request);
        }

        private async Task SubmitAsync()
        {
            try
            {
                await ApiClient.CreateClientAsync(Request, CancellationToken.None);
                Navigation.NavigateTo("/catalog/clients");
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }
    }
}
```

- [ ] **Step 8: Add nav links**

```razor
@* Add to frontends/AzureSuite.Web.Blazor/Layout/NavMenu.razor, alongside the existing catalog/ingestion links: *@
<NavLink href="/catalog/clients" class="app-nav-link">clients</NavLink>
```

- [ ] **Step 9: Run the full Blazor test suite**

Run: `dotnet test tests/AzureSuite.Web.Blazor.Tests`
Expected: PASS, no warnings.

- [ ] **Step 10: Commit**

```bash
git add frontends/AzureSuite.Web.Blazor/Features/Catalog/Contracts/ClientDto.cs frontends/AzureSuite.Web.Blazor/Features/Catalog/Contracts/CreateClientRequest.cs frontends/AzureSuite.Web.Blazor/Features/Catalog/Services/CatalogApiClient.cs frontends/AzureSuite.Web.Blazor/Features/Catalog/Pages/ClientsList.razor frontends/AzureSuite.Web.Blazor/Features/Catalog/Pages/ClientsList.razor.cs frontends/AzureSuite.Web.Blazor/Features/Catalog/Pages/RegisterClient.razor frontends/AzureSuite.Web.Blazor/Features/Catalog/Pages/RegisterClient.razor.cs frontends/AzureSuite.Web.Blazor/Layout/NavMenu.razor tests/AzureSuite.Web.Blazor.Tests/Features/Catalog/Pages/ClientsListTests.cs
git commit -m "feat(web): add Clients management UI"
```

---

### Task 8: Blazor UI — Routes management

**Files:**
- Create: `frontends/AzureSuite.Web.Blazor/Features/Catalog/Contracts/{RouteDto,CreateRouteRequest}.cs`
- Modify: `frontends/AzureSuite.Web.Blazor/Features/Catalog/Services/CatalogApiClient.cs`
- Create: `frontends/AzureSuite.Web.Blazor/Features/Catalog/Pages/RoutesList.razor` + `.razor.cs`
- Create: `frontends/AzureSuite.Web.Blazor/Features/Catalog/Pages/RegisterRoute.razor` + `.razor.cs`
- Modify: `frontends/AzureSuite.Web.Blazor/Layout/NavMenu.razor`
- Test: `tests/AzureSuite.Web.Blazor.Tests/Features/Catalog/Pages/RoutesListTests.cs`

**Interfaces:**
- Consumes: `Catalog.Api`'s `/routes` endpoints from Task 4; `ClientDto` from Task 7 (for the dropdown).
- Produces: `CatalogApiClient.GetRoutesAsync`, `.CreateRouteAsync`; route `/catalog/routes`, `/catalog/routes/register`.

This task mirrors Task 7's structure exactly, against `Route` instead of `Client`. Full detail below for the parts that differ (the form has more fields and a multi-value queue-names input); the list page and test follow Task 7's `ClientsList`/`ClientsListTests` shape one-for-one.

- [ ] **Step 1: Add web-side contracts**

```csharp
// frontends/AzureSuite.Web.Blazor/Features/Catalog/Contracts/RouteDto.cs
namespace AzureSuite.Web.Blazor.Features.Catalog.Contracts
{
    public record RouteDto(Guid Id, Guid ClientId, string MessageTypeName, string MessageTypeVersion, IReadOnlyList<string> QueueNames);
}
```

```csharp
// frontends/AzureSuite.Web.Blazor/Features/Catalog/Contracts/CreateRouteRequest.cs
namespace AzureSuite.Web.Blazor.Features.Catalog.Contracts
{
    public record CreateRouteRequest(Guid ClientId, string MessageTypeName, string MessageTypeVersion, IReadOnlyList<string> QueueNames);
}
```

- [ ] **Step 2: Write the failing page test**

```csharp
// tests/AzureSuite.Web.Blazor.Tests/Features/Catalog/Pages/RoutesListTests.cs
using AzureSuite.Web.Blazor.Features.Catalog.Pages;
using AzureSuite.Web.Blazor.Features.Catalog.Services;
using Bunit;
using FluentAssertions;
using Xunit;

namespace AzureSuite.Web.Blazor.Tests.Features.Catalog.Pages
{
    public class RoutesListTests : BunitContext
    {
        public RoutesListTests()
        {
            Services.AddScoped(_ => new CatalogApiClient(new HttpClient { BaseAddress = new Uri("https://localhost/") }));
        }

        [Fact]
        public void RendersHeading()
        {
            var cut = Render<RoutesList>();

            cut.Find("h1").TextContent.Should().Be("Routes");
        }
    }
}
```

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test tests/AzureSuite.Web.Blazor.Tests --filter RoutesListTests`
Expected: FAIL

- [ ] **Step 4: Add `CatalogApiClient` methods**

```csharp
// Add to frontends/AzureSuite.Web.Blazor/Features/Catalog/Services/CatalogApiClient.cs
public async Task<IReadOnlyList<RouteDto>> GetRoutesAsync(CancellationToken cancellationToken)
{
    var result = await _httpClient.GetFromJsonAsync<List<RouteDto>>("/routes", cancellationToken);
    return result ?? new List<RouteDto>();
}

public async Task<RouteDto> CreateRouteAsync(CreateRouteRequest request, CancellationToken cancellationToken)
{
    var response = await _httpClient.PostAsJsonAsync("/routes", request, cancellationToken);
    response.EnsureSuccessStatusCode();
    return (await response.Content.ReadFromJsonAsync<RouteDto>(cancellationToken))!;
}
```

- [ ] **Step 5: Implement `RoutesList` page**

```razor
@* frontends/AzureSuite.Web.Blazor/Features/Catalog/Pages/RoutesList.razor *@
@page "/catalog/routes"

<PageHeader Title="Routes" Eyebrow="Catalog">
    <Actions>
        <AppButton Href="/catalog/routes/register">Register route</AppButton>
    </Actions>
</PageHeader>

@if (Routes is null)
{
    <p>Loading...</p>
}
else if (Routes.Count == 0)
{
    <p class="subtitle">No routes registered yet.</p>
}
else
{
    <table class="data-table">
        <thead>
            <tr><th>Client</th><th>Message type</th><th>Version</th><th>Queues</th></tr>
        </thead>
        <tbody>
            @foreach (var route in Routes)
            {
                <tr>
                    <td>@route.ClientId</td>
                    <td>@route.MessageTypeName</td>
                    <td>@route.MessageTypeVersion</td>
                    <td>@string.Join(", ", route.QueueNames)</td>
                </tr>
            }
        </tbody>
    </table>
}
```

```csharp
// frontends/AzureSuite.Web.Blazor/Features/Catalog/Pages/RoutesList.razor.cs
using AzureSuite.Web.Blazor.Features.Catalog.Contracts;
using AzureSuite.Web.Blazor.Features.Catalog.Services;
using Microsoft.AspNetCore.Components;

namespace AzureSuite.Web.Blazor.Features.Catalog.Pages
{
    public partial class RoutesList : ComponentBase
    {
        [Inject] public CatalogApiClient ApiClient { get; set; } = default!;

        private IReadOnlyList<RouteDto>? Routes;

        protected override async Task OnInitializedAsync()
        {
            Routes = await ApiClient.GetRoutesAsync(CancellationToken.None);
        }
    }
}
```

- [ ] **Step 6: Run test to verify it passes**

Run: `dotnet test tests/AzureSuite.Web.Blazor.Tests --filter RoutesListTests`
Expected: PASS

- [ ] **Step 7: Implement `RegisterRoute` page**

```razor
@* frontends/AzureSuite.Web.Blazor/Features/Catalog/Pages/RegisterRoute.razor *@
@page "/catalog/routes/register"

<PageHeader Title="Register Route" Eyebrow="Catalog">
    <Actions>
        <AppButton Href="/catalog/routes" Variant="AppButtonVariant.Secondary">Back to list</AppButton>
    </Actions>
</PageHeader>

<EditForm EditContext="@EditContext" OnValidSubmit="@SubmitAsync">
    <div class="form-group">
        <label>Client Id</label>
        <InputText @bind-Value="ClientIdText" />
    </div>
    <div class="form-group">
        <label>Message Type</label>
        <InputText @bind-Value="MessageTypeName" />
    </div>
    <div class="form-group">
        <label>Version</label>
        <InputText @bind-Value="MessageTypeVersion" />
    </div>
    <div class="form-group">
        <label>Queue Names (comma-separated)</label>
        <InputText @bind-Value="QueueNamesText" />
    </div>
    <AppButton Type="submit">Register</AppButton>
</EditForm>

@if (ErrorMessage is not null)
{
    <ErrorState Message="@ErrorMessage" />
}
```

```csharp
// frontends/AzureSuite.Web.Blazor/Features/Catalog/Pages/RegisterRoute.razor.cs
using AzureSuite.Web.Blazor.Features.Catalog.Contracts;
using AzureSuite.Web.Blazor.Features.Catalog.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace AzureSuite.Web.Blazor.Features.Catalog.Pages
{
    public partial class RegisterRoute : ComponentBase
    {
        [Inject] public CatalogApiClient ApiClient { get; set; } = default!;
        [Inject] public NavigationManager Navigation { get; set; } = default!;

        private string ClientIdText = "";
        private string MessageTypeName = "";
        private string MessageTypeVersion = "";
        private string QueueNamesText = "";
        private EditContext EditContext = default!;
        private string? ErrorMessage;

        protected override void OnInitialized()
        {
            EditContext = new EditContext(this);
        }

        private async Task SubmitAsync()
        {
            try
            {
                var queueNames = QueueNamesText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var request = new CreateRouteRequest(Guid.Parse(ClientIdText), MessageTypeName, MessageTypeVersion, queueNames);
                await ApiClient.CreateRouteAsync(request, CancellationToken.None);
                Navigation.NavigateTo("/catalog/routes");
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }
    }
}
```

- [ ] **Step 8: Add nav link**

```razor
@* Add to frontends/AzureSuite.Web.Blazor/Layout/NavMenu.razor: *@
<NavLink href="/catalog/routes" class="app-nav-link">routes</NavLink>
```

- [ ] **Step 9: Run the full Blazor test suite**

Run: `dotnet test tests/AzureSuite.Web.Blazor.Tests`
Expected: PASS, no warnings.

- [ ] **Step 10: Commit**

```bash
git add frontends/AzureSuite.Web.Blazor/Features/Catalog/Contracts/RouteDto.cs frontends/AzureSuite.Web.Blazor/Features/Catalog/Contracts/CreateRouteRequest.cs frontends/AzureSuite.Web.Blazor/Features/Catalog/Services/CatalogApiClient.cs frontends/AzureSuite.Web.Blazor/Features/Catalog/Pages/RoutesList.razor frontends/AzureSuite.Web.Blazor/Features/Catalog/Pages/RoutesList.razor.cs frontends/AzureSuite.Web.Blazor/Features/Catalog/Pages/RegisterRoute.razor frontends/AzureSuite.Web.Blazor/Features/Catalog/Pages/RegisterRoute.razor.cs frontends/AzureSuite.Web.Blazor/Layout/NavMenu.razor tests/AzureSuite.Web.Blazor.Tests/Features/Catalog/Pages/RoutesListTests.cs
git commit -m "feat(web): add Routes management UI"
```

---

### Task 9: Seed test data

**Files:**
- Create: `scripts/seed-routing-test-data.sql`
- Modify: `services/Catalog/Catalog.Api/Persistence/InMemorySeedData.cs`

**Interfaces:**
- Consumes: `Client`, `Route`, `MessageType` from Tasks 1/3/existing.
- Produces: 2 `Client` rows, 3 distinct output queue names across `Route` rows, one `Client` with 1 `Route`, one `Client` with 3 `Route`s — matching the spec's test-data plan.

- [ ] **Step 1: Extend the InMemory seed (local dev)**

```csharp
// services/Catalog/Catalog.Api/Persistence/InMemorySeedData.cs
// Add after the existing MessageTypes seeding, still inside Apply(context), guarded the
// same way (skip if already seeded):
if (!context.Clients.Any())
{
    var clientA = new Client("Contoso Payments");
    var clientB = new Client("Fabrikam Treasury");
    context.Clients.AddRange(clientA, clientB);
    context.SaveChanges();

    context.Routes.AddRange(
        new Route(clientA.Id, new MessageTypeName("pacs.008"), new MessageTypeVersion("1.0"), new[] { "out-settlements" }),
        new Route(clientB.Id, new MessageTypeName("pacs.008"), new MessageTypeVersion("1.0"), new[] { "out-settlements" }),
        new Route(clientB.Id, new MessageTypeName("camt.054"), new MessageTypeVersion("1.0"), new[] { "out-notifications" }),
        new Route(clientB.Id, new MessageTypeName("camt.054"), new MessageTypeVersion("1.0"), new[] { "out-audit" }));
    context.SaveChanges();
}
```

This gives Client A (Contoso) 1 route (`pacs.008` → `out-settlements`) and Client B (Fabrikam) 3 routes (`pacs.008` → `out-settlements`, `camt.054` → `out-notifications`, `camt.054` → `out-audit`), across exactly 3 distinct queue names — matching the spec's test-data plan.

- [ ] **Step 2: Write the equivalent SQL seed script (real SQL Server deployment)**

```sql
-- scripts/seed-routing-test-data.sql
-- Run against the deployed Catalog SQL database after migrations have been applied.
-- Idempotent: skips seeding if a client already exists.

IF NOT EXISTS (SELECT 1 FROM Clients)
BEGIN
    DECLARE @ClientAId UNIQUEIDENTIFIER = NEWID();
    DECLARE @ClientBId UNIQUEIDENTIFIER = NEWID();

    INSERT INTO Clients (Id, Name, RegisteredAtUtc)
    VALUES
        (@ClientAId, 'Contoso Payments', SYSUTCDATETIME()),
        (@ClientBId, 'Fabrikam Treasury', SYSUTCDATETIME());

    INSERT INTO Routes (Id, ClientId, MessageTypeName, MessageTypeVersion, QueueNames)
    VALUES
        (NEWID(), @ClientAId, 'pacs.008', '1.0', N'["out-settlements"]'),
        (NEWID(), @ClientBId, 'pacs.008', '1.0', N'["out-settlements"]'),
        (NEWID(), @ClientBId, 'camt.054', '1.0', N'["out-notifications"]'),
        (NEWID(), @ClientBId, 'camt.054', '1.0', N'["out-audit"]');
END
```

Note: `QueueNames`'s exact column name/shape depends on how Task 3's `PrimitiveCollection` mapping actually materializes in the generated migration — confirm the column name matches by inspecting the migration file generated in Task 3 Step 6 before running this script; adjust the JSON literal syntax if EF Core named or shaped the column differently.

- [ ] **Step 3: Verify the InMemory seed manually**

Run: `dotnet run --project services/Catalog/Catalog.Api` (with no `ConnectionStrings:CatalogDb` configured, so it uses the InMemory provider), then `GET /clients` and `GET /routes` via the Scalar UI at `/scalar` — confirm 2 clients and 4 routes across 3 distinct queue names come back.

- [ ] **Step 4: Commit**

```bash
git add services/Catalog/Catalog.Api/Persistence/InMemorySeedData.cs scripts/seed-routing-test-data.sql
git commit -m "feat(catalog): seed Client/Route test data"
```

---

## Self-Review

**Spec coverage:**
- `Client`/`Route` entities, full CRUD, single-purpose validate + route-lookup endpoints → Tasks 1–6.
- UI for managing Clients and Routes → Tasks 7–8.
- Test data (2 clients, 3 queues, 1-route/3-route split) → Task 9.
- Out of this plan, by design: `Processing.Functions`, the `Ingestion.Api` `ClientId` contract change, Cosmos, output-queue infra, and CI/CD — all depend on this plan's endpoints existing first and are covered by a follow-up plan once this one is implemented and merged.

**Placeholder scan:** no TBD/TODO; every step has concrete code; Update/Delete handlers for both entities are written out in full rather than referenced as "similar to Create."

**Type consistency:** `ClientDto`/`RouteDto` field names and `IClientRepository`/`IRouteRepository` method signatures introduced in Tasks 1–4 are reused verbatim in Tasks 5–9 (`ListByClientAndTypeAsync`, `QueueNames`, etc.) — no renames between tasks.

---

Plan complete and saved to `docs/superpowers/plans/2026-09-23-catalog-client-route.md`. Two execution options:

**1. Subagent-Driven (recommended)** - I dispatch a fresh subagent per task, review between tasks, fast iteration

**2. Inline Execution** - Execute tasks in this session using executing-plans, batch execution with checkpoints

**Which approach?**
