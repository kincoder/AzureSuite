# Catalog Service Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stand up the Catalog service — the registry of message types, schema
versions, subscriber registrations, and routing rules that every other
service in the financial messaging hub depends on — as the first working
piece of the rebuilt solution.

**Architecture:** Catalog.Domain (entities) ← Catalog.Application (CQRS
commands/queries via MediatR, repository abstraction) ← Catalog.Infrastructure
(EF Core / SQL Server) → Catalog.Api (minimal API). Lightweight CQRS: commands
and queries are separate handler classes but both read/write the same
database — no separate read model yet, per the design spec.

**Tech Stack:** .NET 10, ASP.NET Core minimal APIs, EF Core (SQL Server
provider for real use, InMemory provider for infrastructure tests), MediatR,
xUnit, FluentAssertions, Bicep.

**Spec:** `docs/superpowers/specs/2026-09-11-financial-messaging-hub-design.md`

> **Note (mid-execution update):** Tasks 2-5 below were implemented with two
> deviations from the code samples as originally written, agreed with the user
> during execution: (1) `MessageType.Name`/`Version` are `MessageTypeName`/
> `MessageTypeVersion` value objects (own validation, value equality), not raw
> strings — `IMessageTypeRepository` and all handlers take/return the value
> objects, with primitives only at the `RegisterMessageTypeCommand`/
> `GetMessageTypeQuery`/`MessageTypeDto` boundary. (2) every file uses
> block-scoped namespaces and XML doc comments per Global Constraints below.
> The actual committed code is the source of truth; Task 6 below has been
> updated to match it, Tasks 2-5's embedded snippets have not been
> retroactively edited.

## Global Constraints

- Hand-written code must be warning-free, nullable-enabled, no `#pragma
  warning disable` (EF-generated migration files are the only exception and
  must never be hand-edited).
- Every `services/X` project gets a matching `tests/X.Tests` project mirroring
  its folder structure 1:1, one test class per production class. xUnit +
  FluentAssertions + coverlet.collector.
- Solution-wide `.slnx`, not per-service solution files.
- Repo layout root: `services/Catalog/`, `tests/`, `infra/modules/catalog/`.
- New Azure resource group for this rebuild (not `rg-azuresuite-dev`) —
  naming decided in Task 7.
- **Namespaces are block-scoped** (`namespace X { }`), never file-scoped
  (`namespace X;`) — applies to every `.cs` file, including tests.
- **No top-level statements.** Every `Program.cs` has an explicit
  `public class Program` with `static void Main(string[] args)`.
- **API style: minimal API endpoints**, not controllers — routes registered
  via `app.MapGet`/`app.MapPost` etc. inside `Main`, not `[ApiController]`
  classes.
- **XML doc comments** (`/// <summary>`) on every class/record and on any
  property whose purpose isn't obvious from its name alone (e.g. what a
  schema/definition field actually holds) — supports future OpenAPI/help
  generation. Trivial properties (an `Id`, a DTO field that just mirrors an
  entity property) don't need one if the class-level summary already makes
  the shape clear.

---

## Task 1: Solution & project scaffold

**Files:**
- Create: `AzureSuite.slnx`
- Create: `services/Catalog/Catalog.Domain/Catalog.Domain.csproj`
- Create: `services/Catalog/Catalog.Application/Catalog.Application.csproj`
- Create: `services/Catalog/Catalog.Infrastructure/Catalog.Infrastructure.csproj`
- Create: `services/Catalog/Catalog.Api/Catalog.Api.csproj`
- Create: `tests/Catalog.Domain.Tests/Catalog.Domain.Tests.csproj`
- Create: `tests/Catalog.Application.Tests/Catalog.Application.Tests.csproj`
- Create: `tests/Catalog.Infrastructure.Tests/Catalog.Infrastructure.Tests.csproj`

**Interfaces:**
- Produces: a solution that builds and a placeholder test in each test
  project that passes, proving the project graph (Domain ← Application ←
  Infrastructure, Api → all three) is wired correctly before any real code
  exists.

- [ ] **Step 1: Create the solution file**

Run:
```bash
dotnet new sln -n AzureSuite --format slnx
```

- [ ] **Step 2: Scaffold the four service projects**

Run:
```bash
dotnet new classlib -n Catalog.Domain -o services/Catalog/Catalog.Domain
dotnet new classlib -n Catalog.Application -o services/Catalog/Catalog.Application
dotnet new classlib -n Catalog.Infrastructure -o services/Catalog/Catalog.Infrastructure
dotnet new webapi -n Catalog.Api -o services/Catalog/Catalog.Api --use-minimal-apis
```

- [ ] **Step 3: Scaffold the three test projects**

Run:
```bash
dotnet new xunit -n Catalog.Domain.Tests -o tests/Catalog.Domain.Tests
dotnet new xunit -n Catalog.Application.Tests -o tests/Catalog.Application.Tests
dotnet new xunit -n Catalog.Infrastructure.Tests -o tests/Catalog.Infrastructure.Tests
```

- [ ] **Step 4: Add project references**

Run:
```bash
dotnet add services/Catalog/Catalog.Application reference services/Catalog/Catalog.Domain
dotnet add services/Catalog/Catalog.Infrastructure reference services/Catalog/Catalog.Application
dotnet add services/Catalog/Catalog.Api reference services/Catalog/Catalog.Application
dotnet add services/Catalog/Catalog.Api reference services/Catalog/Catalog.Infrastructure

dotnet add tests/Catalog.Domain.Tests reference services/Catalog/Catalog.Domain
dotnet add tests/Catalog.Application.Tests reference services/Catalog/Catalog.Application
dotnet add tests/Catalog.Infrastructure.Tests reference services/Catalog/Catalog.Infrastructure
```

- [ ] **Step 5: Add FluentAssertions to every test project**

Run:
```bash
dotnet add tests/Catalog.Domain.Tests package FluentAssertions
dotnet add tests/Catalog.Application.Tests package FluentAssertions
dotnet add tests/Catalog.Infrastructure.Tests package FluentAssertions
```

- [ ] **Step 6: Add all projects to the solution**

Run:
```bash
dotnet sln AzureSuite.slnx add services/Catalog/Catalog.Domain services/Catalog/Catalog.Application services/Catalog/Catalog.Infrastructure services/Catalog/Catalog.Api tests/Catalog.Domain.Tests tests/Catalog.Application.Tests tests/Catalog.Infrastructure.Tests
```

- [ ] **Step 7: Delete template cruft**

The `webapi` template scaffolds a `WeatherForecast.cs` sample — remove it and
its usage in `Program.cs` (leave `Program.cs` as the bare minimal-API
template with the weather endpoint removed, just `app.Run();` at the end).

Delete: `services/Catalog/Catalog.Api/WeatherForecast.cs`

Edit `services/Catalog/Catalog.Api/Program.cs` to remove the
`/weatherforecast` endpoint block and the `WeatherForecast` record usage,
keeping only:
```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.Run();
```

- [ ] **Step 8: Confirm the solution builds**

Run: `dotnet build AzureSuite.slnx`
Expected: Build succeeded, 0 warnings, 0 errors.

- [ ] **Step 9: Commit**

```bash
git add AzureSuite.slnx services/Catalog tests/Catalog.Domain.Tests tests/Catalog.Application.Tests tests/Catalog.Infrastructure.Tests
git commit -m "Scaffold Catalog service project structure"
```

---

## Task 2: Domain — MessageType entity

**Files:**
- Create: `services/Catalog/Catalog.Domain/Entities/MessageType.cs`
- Test: `tests/Catalog.Domain.Tests/Entities/MessageTypeTests.cs`

**Interfaces:**
- Produces: `MessageType` — constructor `MessageType(string name, string
  version, string schemaDefinition)`, read-only properties `Id` (Guid),
  `Name` (string), `Version` (string), `SchemaDefinition` (string),
  `RegisteredAtUtc` (DateTime). Throws `ArgumentException` if `name`,
  `version`, or `schemaDefinition` is null/empty/whitespace.

- [ ] **Step 1: Write the failing tests**

```csharp
using AzureSuite.Catalog.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Catalog.Domain.Tests.Entities;

public class MessageTypeTests
{
    [Fact]
    public void Constructor_WithValidArguments_SetsProperties()
    {
        var messageType = new MessageType("pacs.008", "1.0", "{ \"type\": \"object\" }");

        messageType.Name.Should().Be("pacs.008");
        messageType.Version.Should().Be("1.0");
        messageType.SchemaDefinition.Should().Be("{ \"type\": \"object\" }");
        messageType.Id.Should().NotBeEmpty();
        messageType.RegisteredAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidName_ThrowsArgumentException(string? invalidName)
    {
        var act = () => new MessageType(invalidName!, "1.0", "{}");

        act.Should().Throw<ArgumentException>().WithParameterName("name");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidVersion_ThrowsArgumentException(string? invalidVersion)
    {
        var act = () => new MessageType("pacs.008", invalidVersion!, "{}");

        act.Should().Throw<ArgumentException>().WithParameterName("version");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidSchemaDefinition_ThrowsArgumentException(string? invalidSchema)
    {
        var act = () => new MessageType("pacs.008", "1.0", invalidSchema!);

        act.Should().Throw<ArgumentException>().WithParameterName("schemaDefinition");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Catalog.Domain.Tests`
Expected: FAIL to compile — `MessageType` doesn't exist yet.

- [ ] **Step 3: Implement `MessageType`**

```csharp
namespace AzureSuite.Catalog.Domain.Entities;

public class MessageType
{
    public Guid Id { get; }
    public string Name { get; }
    public string Version { get; }
    public string SchemaDefinition { get; }
    public DateTime RegisteredAtUtc { get; }

    public MessageType(string name, string version, string schemaDefinition)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(version))
        {
            throw new ArgumentException("Version is required.", nameof(version));
        }

        if (string.IsNullOrWhiteSpace(schemaDefinition))
        {
            throw new ArgumentException("Schema definition is required.", nameof(schemaDefinition));
        }

        Id = Guid.NewGuid();
        Name = name;
        Version = version;
        SchemaDefinition = schemaDefinition;
        RegisteredAtUtc = DateTime.UtcNow;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Catalog.Domain.Tests`
Expected: PASS, all 7 test cases green.

- [ ] **Step 5: Commit**

```bash
git add services/Catalog/Catalog.Domain/Entities/MessageType.cs tests/Catalog.Domain.Tests/Entities/MessageTypeTests.cs
git commit -m "Add MessageType domain entity"
```

---

## Task 3: Infrastructure — EF Core persistence

**Files:**
- Create: `services/Catalog/Catalog.Application/Abstractions/IMessageTypeRepository.cs`
- Create: `services/Catalog/Catalog.Infrastructure/Persistence/CatalogDbContext.cs`
- Create: `services/Catalog/Catalog.Infrastructure/Persistence/Repositories/MessageTypeRepository.cs`
- Test: `tests/Catalog.Infrastructure.Tests/Persistence/Repositories/MessageTypeRepositoryTests.cs`

**Interfaces:**
- Consumes: `MessageType` from Task 2 (`Id`, `Name`, `Version`,
  `SchemaDefinition`, `RegisteredAtUtc`).
- Produces: `IMessageTypeRepository` with `Task AddAsync(MessageType
  messageType, CancellationToken cancellationToken)`, `Task<MessageType?>
  GetByNameAndVersionAsync(string name, string version, CancellationToken
  cancellationToken)`, `Task<IReadOnlyList<MessageType>> ListAsync(CancellationToken
  cancellationToken)`. `CatalogDbContext` with `DbSet<MessageType>
  MessageTypes`. `MessageTypeRepository : IMessageTypeRepository`
  constructed with `CatalogDbContext`.

- [ ] **Step 1: Add EF Core packages**

Run:
```bash
dotnet add services/Catalog/Catalog.Infrastructure package Microsoft.EntityFrameworkCore.SqlServer
dotnet add services/Catalog/Catalog.Infrastructure package Microsoft.EntityFrameworkCore.Design
dotnet add tests/Catalog.Infrastructure.Tests package Microsoft.EntityFrameworkCore.InMemory
```

- [ ] **Step 2: Define the repository abstraction (Application layer)**

```csharp
using AzureSuite.Catalog.Domain.Entities;

namespace AzureSuite.Catalog.Application.Abstractions;

public interface IMessageTypeRepository
{
    Task AddAsync(MessageType messageType, CancellationToken cancellationToken);

    Task<MessageType?> GetByNameAndVersionAsync(string name, string version, CancellationToken cancellationToken);

    Task<IReadOnlyList<MessageType>> ListAsync(CancellationToken cancellationToken);
}
```

- [ ] **Step 3: Write the failing repository test**

```csharp
using AzureSuite.Catalog.Domain.Entities;
using AzureSuite.Catalog.Infrastructure.Persistence;
using AzureSuite.Catalog.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Catalog.Infrastructure.Tests.Persistence.Repositories;

public class MessageTypeRepositoryTests
{
    private static CatalogDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new CatalogDbContext(options);
    }

    [Fact]
    public async Task AddAsync_ThenGetByNameAndVersionAsync_ReturnsTheSameMessageType()
    {
        await using var context = CreateContext();
        var repository = new MessageTypeRepository(context);
        var messageType = new MessageType("pacs.008", "1.0", "{}");

        await repository.AddAsync(messageType, CancellationToken.None);
        var found = await repository.GetByNameAndVersionAsync("pacs.008", "1.0", CancellationToken.None);

        found.Should().NotBeNull();
        found!.Id.Should().Be(messageType.Id);
    }

    [Fact]
    public async Task GetByNameAndVersionAsync_WhenNotFound_ReturnsNull()
    {
        await using var context = CreateContext();
        var repository = new MessageTypeRepository(context);

        var found = await repository.GetByNameAndVersionAsync("camt.054", "1.0", CancellationToken.None);

        found.Should().BeNull();
    }

    [Fact]
    public async Task ListAsync_ReturnsAllRegisteredMessageTypes()
    {
        await using var context = CreateContext();
        var repository = new MessageTypeRepository(context);
        await repository.AddAsync(new MessageType("pacs.008", "1.0", "{}"), CancellationToken.None);
        await repository.AddAsync(new MessageType("camt.054", "1.0", "{}"), CancellationToken.None);

        var all = await repository.ListAsync(CancellationToken.None);

        all.Should().HaveCount(2);
        all.Select(m => m.Name).Should().BeEquivalentTo("pacs.008", "camt.054");
    }
}
```

- [ ] **Step 4: Run tests to verify they fail**

Run: `dotnet test tests/Catalog.Infrastructure.Tests`
Expected: FAIL to compile — `CatalogDbContext`/`MessageTypeRepository` don't exist yet.

- [ ] **Step 5: Implement `CatalogDbContext`**

```csharp
using AzureSuite.Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AzureSuite.Catalog.Infrastructure.Persistence;

public class CatalogDbContext : DbContext
{
    public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options)
    {
    }

    public DbSet<MessageType> MessageTypes => Set<MessageType>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MessageType>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.HasIndex(m => new { m.Name, m.Version }).IsUnique();
            entity.Property(m => m.Name).IsRequired().HasMaxLength(100);
            entity.Property(m => m.Version).IsRequired().HasMaxLength(20);
            entity.Property(m => m.SchemaDefinition).IsRequired();
        });
    }
}
```

- [ ] **Step 6: Implement `MessageTypeRepository`**

```csharp
using AzureSuite.Catalog.Application.Abstractions;
using AzureSuite.Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AzureSuite.Catalog.Infrastructure.Persistence.Repositories;

public class MessageTypeRepository : IMessageTypeRepository
{
    private readonly CatalogDbContext _context;

    public MessageTypeRepository(CatalogDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(MessageType messageType, CancellationToken cancellationToken)
    {
        _context.MessageTypes.Add(messageType);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task<MessageType?> GetByNameAndVersionAsync(string name, string version, CancellationToken cancellationToken)
    {
        return _context.MessageTypes
            .FirstOrDefaultAsync(m => m.Name == name && m.Version == version, cancellationToken);
    }

    public async Task<IReadOnlyList<MessageType>> ListAsync(CancellationToken cancellationToken)
    {
        return await _context.MessageTypes.ToListAsync(cancellationToken);
    }
}
```

- [ ] **Step 7: Run tests to verify they pass**

Run: `dotnet test tests/Catalog.Infrastructure.Tests`
Expected: PASS, all 3 tests green.

- [ ] **Step 8: Commit**

```bash
git add services/Catalog/Catalog.Application/Abstractions services/Catalog/Catalog.Infrastructure tests/Catalog.Infrastructure.Tests
git commit -m "Add Catalog EF Core persistence and MessageTypeRepository"
```

---

## Task 4: Application — RegisterMessageType command

**Files:**
- Create: `services/Catalog/Catalog.Application/MessageTypes/MessageTypeDto.cs`
- Create: `services/Catalog/Catalog.Application/MessageTypes/Commands/RegisterMessageType/RegisterMessageTypeCommand.cs`
- Create: `services/Catalog/Catalog.Application/MessageTypes/Commands/RegisterMessageType/RegisterMessageTypeHandler.cs`
- Test: `tests/Catalog.Application.Tests/TestDoubles/FakeMessageTypeRepository.cs`
- Test: `tests/Catalog.Application.Tests/MessageTypes/Commands/RegisterMessageType/RegisterMessageTypeHandlerTests.cs`

**Interfaces:**
- Consumes: `IMessageTypeRepository` (Task 3), `MessageType` (Task 2).
- Produces: `MessageTypeDto(Guid Id, string Name, string Version, string
  SchemaDefinition, DateTime RegisteredAtUtc)` (record). `RegisterMessageTypeCommand(string
  Name, string Version, string SchemaDefinition) : IRequest<MessageTypeDto>`.
  `RegisterMessageTypeHandler : IRequestHandler<RegisterMessageTypeCommand,
  MessageTypeDto>` — throws `InvalidOperationException` if a message type
  with the same Name+Version is already registered.

- [ ] **Step 1: Add MediatR package**

Run:
```bash
dotnet add services/Catalog/Catalog.Application package MediatR
dotnet add services/Catalog/Catalog.Api package MediatR
```

- [ ] **Step 2: Write the fake repository test double**

```csharp
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
```

- [ ] **Step 3: Write the failing handler test**

```csharp
using AzureSuite.Catalog.Application.MessageTypes.Commands.RegisterMessageType;
using Catalog.Application.Tests.TestDoubles;
using FluentAssertions;
using Xunit;

namespace Catalog.Application.Tests.MessageTypes.Commands.RegisterMessageType;

public class RegisterMessageTypeHandlerTests
{
    [Fact]
    public async Task Handle_WithNewMessageType_RegistersAndReturnsDto()
    {
        var repository = new FakeMessageTypeRepository();
        var handler = new RegisterMessageTypeHandler(repository);
        var command = new RegisterMessageTypeCommand("pacs.008", "1.0", "{}");

        var result = await handler.Handle(command, CancellationToken.None);

        result.Name.Should().Be("pacs.008");
        result.Version.Should().Be("1.0");
        result.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handle_WithDuplicateNameAndVersion_ThrowsInvalidOperationException()
    {
        var repository = new FakeMessageTypeRepository();
        var handler = new RegisterMessageTypeHandler(repository);
        var command = new RegisterMessageTypeCommand("pacs.008", "1.0", "{}");
        await handler.Handle(command, CancellationToken.None);

        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
```

- [ ] **Step 4: Run tests to verify they fail**

Run: `dotnet test tests/Catalog.Application.Tests`
Expected: FAIL to compile — command/handler/DTO don't exist yet.

- [ ] **Step 5: Implement `MessageTypeDto`**

```csharp
namespace AzureSuite.Catalog.Application.MessageTypes;

public record MessageTypeDto(Guid Id, string Name, string Version, string SchemaDefinition, DateTime RegisteredAtUtc);
```

- [ ] **Step 6: Implement `RegisterMessageTypeCommand`**

```csharp
using MediatR;

namespace AzureSuite.Catalog.Application.MessageTypes.Commands.RegisterMessageType;

public record RegisterMessageTypeCommand(string Name, string Version, string SchemaDefinition) : IRequest<MessageTypeDto>;
```

- [ ] **Step 7: Implement `RegisterMessageTypeHandler`**

```csharp
using AzureSuite.Catalog.Application.Abstractions;
using AzureSuite.Catalog.Domain.Entities;
using MediatR;

namespace AzureSuite.Catalog.Application.MessageTypes.Commands.RegisterMessageType;

public class RegisterMessageTypeHandler : IRequestHandler<RegisterMessageTypeCommand, MessageTypeDto>
{
    private readonly IMessageTypeRepository _repository;

    public RegisterMessageTypeHandler(IMessageTypeRepository repository)
    {
        _repository = repository;
    }

    public async Task<MessageTypeDto> Handle(RegisterMessageTypeCommand request, CancellationToken cancellationToken)
    {
        var existing = await _repository.GetByNameAndVersionAsync(request.Name, request.Version, cancellationToken);
        if (existing is not null)
        {
            throw new InvalidOperationException(
                $"Message type '{request.Name}' version '{request.Version}' is already registered.");
        }

        var messageType = new MessageType(request.Name, request.Version, request.SchemaDefinition);
        await _repository.AddAsync(messageType, cancellationToken);

        return new MessageTypeDto(messageType.Id, messageType.Name, messageType.Version, messageType.SchemaDefinition, messageType.RegisteredAtUtc);
    }
}
```

- [ ] **Step 8: Run tests to verify they pass**

Run: `dotnet test tests/Catalog.Application.Tests`
Expected: PASS, both tests green.

- [ ] **Step 9: Commit**

```bash
git add services/Catalog/Catalog.Application tests/Catalog.Application.Tests
git commit -m "Add RegisterMessageType command"
```

---

## Task 5: Application — GetMessageType and ListMessageTypes queries

**Files:**
- Create: `services/Catalog/Catalog.Application/MessageTypes/Queries/GetMessageType/GetMessageTypeQuery.cs`
- Create: `services/Catalog/Catalog.Application/MessageTypes/Queries/GetMessageType/GetMessageTypeHandler.cs`
- Create: `services/Catalog/Catalog.Application/MessageTypes/Queries/ListMessageTypes/ListMessageTypesQuery.cs`
- Create: `services/Catalog/Catalog.Application/MessageTypes/Queries/ListMessageTypes/ListMessageTypesHandler.cs`
- Test: `tests/Catalog.Application.Tests/MessageTypes/Queries/GetMessageType/GetMessageTypeHandlerTests.cs`
- Test: `tests/Catalog.Application.Tests/MessageTypes/Queries/ListMessageTypes/ListMessageTypesHandlerTests.cs`

**Interfaces:**
- Consumes: `IMessageTypeRepository`, `MessageTypeDto`, `FakeMessageTypeRepository` (Task 4).
- Produces: `GetMessageTypeQuery(string Name, string Version) :
  IRequest<MessageTypeDto?>`. `ListMessageTypesQuery() :
  IRequest<IReadOnlyList<MessageTypeDto>>`.

- [ ] **Step 1: Write the failing GetMessageType test**

```csharp
using AzureSuite.Catalog.Application.MessageTypes.Queries.GetMessageType;
using Catalog.Application.Tests.TestDoubles;
using FluentAssertions;
using Xunit;

namespace Catalog.Application.Tests.MessageTypes.Queries.GetMessageType;

public class GetMessageTypeHandlerTests
{
    [Fact]
    public async Task Handle_WhenMessageTypeExists_ReturnsDto()
    {
        var repository = new FakeMessageTypeRepository();
        await repository.AddAsync(new AzureSuite.Catalog.Domain.Entities.MessageType("pacs.008", "1.0", "{}"), CancellationToken.None);
        var handler = new GetMessageTypeHandler(repository);

        var result = await handler.Handle(new GetMessageTypeQuery("pacs.008", "1.0"), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Name.Should().Be("pacs.008");
    }

    [Fact]
    public async Task Handle_WhenMessageTypeDoesNotExist_ReturnsNull()
    {
        var repository = new FakeMessageTypeRepository();
        var handler = new GetMessageTypeHandler(repository);

        var result = await handler.Handle(new GetMessageTypeQuery("camt.054", "1.0"), CancellationToken.None);

        result.Should().BeNull();
    }
}
```

- [ ] **Step 2: Write the failing ListMessageTypes test**

```csharp
using AzureSuite.Catalog.Application.MessageTypes.Queries.ListMessageTypes;
using AzureSuite.Catalog.Domain.Entities;
using Catalog.Application.Tests.TestDoubles;
using FluentAssertions;
using Xunit;

namespace Catalog.Application.Tests.MessageTypes.Queries.ListMessageTypes;

public class ListMessageTypesHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsAllRegisteredMessageTypesAsDtos()
    {
        var repository = new FakeMessageTypeRepository();
        await repository.AddAsync(new MessageType("pacs.008", "1.0", "{}"), CancellationToken.None);
        await repository.AddAsync(new MessageType("camt.054", "1.0", "{}"), CancellationToken.None);
        var handler = new ListMessageTypesHandler(repository);

        var result = await handler.Handle(new ListMessageTypesQuery(), CancellationToken.None);

        result.Should().HaveCount(2);
        result.Select(m => m.Name).Should().BeEquivalentTo("pacs.008", "camt.054");
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test tests/Catalog.Application.Tests`
Expected: FAIL to compile — queries/handlers don't exist yet.

- [ ] **Step 4: Implement `GetMessageTypeQuery` and handler**

```csharp
using MediatR;

namespace AzureSuite.Catalog.Application.MessageTypes.Queries.GetMessageType;

public record GetMessageTypeQuery(string Name, string Version) : IRequest<MessageTypeDto?>;
```

```csharp
using AzureSuite.Catalog.Application.Abstractions;
using MediatR;

namespace AzureSuite.Catalog.Application.MessageTypes.Queries.GetMessageType;

public class GetMessageTypeHandler : IRequestHandler<GetMessageTypeQuery, MessageTypeDto?>
{
    private readonly IMessageTypeRepository _repository;

    public GetMessageTypeHandler(IMessageTypeRepository repository)
    {
        _repository = repository;
    }

    public async Task<MessageTypeDto?> Handle(GetMessageTypeQuery request, CancellationToken cancellationToken)
    {
        var messageType = await _repository.GetByNameAndVersionAsync(request.Name, request.Version, cancellationToken);
        if (messageType is null)
        {
            return null;
        }

        return new MessageTypeDto(messageType.Id, messageType.Name, messageType.Version, messageType.SchemaDefinition, messageType.RegisteredAtUtc);
    }
}
```

- [ ] **Step 5: Implement `ListMessageTypesQuery` and handler**

```csharp
using MediatR;

namespace AzureSuite.Catalog.Application.MessageTypes.Queries.ListMessageTypes;

public record ListMessageTypesQuery : IRequest<IReadOnlyList<MessageTypeDto>>;
```

```csharp
using AzureSuite.Catalog.Application.Abstractions;
using MediatR;

namespace AzureSuite.Catalog.Application.MessageTypes.Queries.ListMessageTypes;

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
            .Select(m => new MessageTypeDto(m.Id, m.Name, m.Version, m.SchemaDefinition, m.RegisteredAtUtc))
            .ToList();
    }
}
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test tests/Catalog.Application.Tests`
Expected: PASS, all tests green (4 new + 2 from Task 4 = 6 total).

- [ ] **Step 7: Commit**

```bash
git add services/Catalog/Catalog.Application/MessageTypes/Queries tests/Catalog.Application.Tests/MessageTypes/Queries
git commit -m "Add GetMessageType and ListMessageTypes queries"
```

---

## Task 6: API — minimal API endpoints

**Files:**
- Modify: `services/Catalog/Catalog.Api/Program.cs`
- Create: `services/Catalog/Catalog.Api/appsettings.json` (already exists from template — modify)
- Test: `tests/Catalog.Api.Tests/Catalog.Api.Tests.csproj`
- Test: `tests/Catalog.Api.Tests/MessageTypesEndpointsTests.cs`

**Interfaces:**
- Consumes: `RegisterMessageTypeCommand`, `GetMessageTypeQuery`,
  `ListMessageTypesQuery`, `MessageTypeDto`, `IMessageTypeRepository`,
  `MessageTypeRepository`, `CatalogDbContext` (Tasks 3-5).
- Produces: `POST /message-types` (body: `{name, version, schemaDefinition}`,
  returns 201 with `MessageTypeDto`), `GET
  /message-types/{name}/{version}` (returns 200 with `MessageTypeDto` or
  404), `GET /message-types` (returns 200 with
  `IReadOnlyList<MessageTypeDto>`).

- [ ] **Step 1: Scaffold the API test project**

Run:
```bash
dotnet new xunit -n Catalog.Api.Tests -o tests/Catalog.Api.Tests
dotnet add tests/Catalog.Api.Tests reference services/Catalog/Catalog.Api
dotnet add tests/Catalog.Api.Tests package FluentAssertions
dotnet add tests/Catalog.Api.Tests package Microsoft.AspNetCore.Mvc.Testing
dotnet sln AzureSuite.slnx add tests/Catalog.Api.Tests
```

- [ ] **Step 2: Write the failing integration test**

```csharp
using System.Net;
using System.Net.Http.Json;
using AzureSuite.Catalog.Api;
using AzureSuite.Catalog.Application.MessageTypes;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Catalog.Api.Tests
{
    public class MessageTypesEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly HttpClient _client;

        public MessageTypesEndpointsTests(WebApplicationFactory<Program> factory)
        {
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task RegisterThenGet_ReturnsTheRegisteredMessageType()
        {
            var registerResponse = await _client.PostAsJsonAsync("/message-types", new
            {
                name = "pacs.008",
                version = "1.0",
                schemaDefinition = "{}"
            });

            registerResponse.StatusCode.Should().Be(HttpStatusCode.Created);
            var registered = await registerResponse.Content.ReadFromJsonAsync<MessageTypeDto>();

            var getResponse = await _client.GetAsync("/message-types/pacs.008/1.0");
            getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var fetched = await getResponse.Content.ReadFromJsonAsync<MessageTypeDto>();

            fetched!.Id.Should().Be(registered!.Id);
        }

        [Fact]
        public async Task Get_WhenMessageTypeDoesNotExist_ReturnsNotFound()
        {
            var response = await _client.GetAsync("/message-types/does-not-exist/1.0");

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task List_ReturnsAllRegisteredMessageTypes()
        {
            await _client.PostAsJsonAsync("/message-types", new { name = "pacs.008", version = "2.0", schemaDefinition = "{}" });
            await _client.PostAsJsonAsync("/message-types", new { name = "camt.054", version = "1.0", schemaDefinition = "{}" });

            var response = await _client.GetAsync("/message-types");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var all = await response.Content.ReadFromJsonAsync<List<MessageTypeDto>>();
            all!.Select(m => m.Name).Should().Contain(new[] { "pacs.008", "camt.054" });
        }
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test tests/Catalog.Api.Tests`
Expected: FAIL — endpoints don't exist / DI not configured (`Program` is
already a public class from Task 1, so `WebApplicationFactory<Program>`
resolves fine — no partial-class trick needed since this project never used
top-level statements).

- [ ] **Step 4: Wire DI and endpoints in `Program.cs`**

```csharp
using AzureSuite.Catalog.Application.Abstractions;
using AzureSuite.Catalog.Application.MessageTypes.Commands.RegisterMessageType;
using AzureSuite.Catalog.Application.MessageTypes.Queries.GetMessageType;
using AzureSuite.Catalog.Application.MessageTypes.Queries.ListMessageTypes;
using AzureSuite.Catalog.Infrastructure.Persistence;
using AzureSuite.Catalog.Infrastructure.Persistence.Repositories;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzureSuite.Catalog.Api
{
    /// <summary>Entry point and endpoint registration for the Catalog service's HTTP API.</summary>
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddOpenApi();
            builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(RegisterMessageTypeCommand).Assembly));
            builder.Services.AddScoped<IMessageTypeRepository, MessageTypeRepository>();

            var connectionString = builder.Configuration.GetConnectionString("CatalogDb");
            if (string.IsNullOrEmpty(connectionString))
            {
                builder.Services.AddDbContext<CatalogDbContext>(options => options.UseInMemoryDatabase("CatalogDb"));
            }
            else
            {
                builder.Services.AddDbContext<CatalogDbContext>(options => options.UseSqlServer(connectionString));
            }

            var app = builder.Build();

            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
            }

            app.UseHttpsRedirection();

            app.MapPost("/message-types", async (RegisterMessageTypeRequest request, IMediator mediator) =>
            {
                var dto = await mediator.Send(new RegisterMessageTypeCommand(request.Name, request.Version, request.SchemaDefinition));
                return Results.Created($"/message-types/{dto.Name}/{dto.Version}", dto);
            });

            app.MapGet("/message-types/{name}/{version}", async (string name, string version, IMediator mediator) =>
            {
                var dto = await mediator.Send(new GetMessageTypeQuery(name, version));
                return dto is null ? Results.NotFound() : Results.Ok(dto);
            });

            app.MapGet("/message-types", async (IMediator mediator) =>
            {
                var dtos = await mediator.Send(new ListMessageTypesQuery());
                return Results.Ok(dtos);
            });

            app.Run();
        }
    }

    /// <summary>Request body for registering a new message type via <c>POST /message-types</c>.</summary>
    public record RegisterMessageTypeRequest(string Name, string Version, string SchemaDefinition);
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/Catalog.Api.Tests`
Expected: PASS, all 3 tests green.

- [ ] **Step 6: Run the full solution test suite**

Run: `dotnet test AzureSuite.slnx`
Expected: PASS, every test project green, 0 warnings.

- [ ] **Step 7: Commit**

```bash
git add services/Catalog/Catalog.Api tests/Catalog.Api.Tests AzureSuite.slnx
git commit -m "Add Catalog minimal API endpoints"
```

---

## Task 7: Infra — Bicep for Catalog SQL + resource group

**Files:**
- Create: `infra/main.bicep`
- Create: `infra/main.dev.bicepparam`
- Create: `infra/modules/catalog/sql.bicep`
- Create: `infra/modules/catalog/keyvault.bicep`

**Interfaces:**
- Produces: a deployable resource group containing a serverless Azure SQL
  database for Catalog and a Key Vault holding its admin password, following
  the same patterns already proven in the superseded build (serverless
  free-tier SQL, RBAC-based Key Vault, `az.getSecret()` in `.bicepparam`).

- [ ] **Step 1: Choose and record the naming convention**

New resource group name: `rg-messaginghub-dev`. Resource naming pattern:
`<type>-messaginghub-catalog-dev` (Key Vault shortened to
`kv-msghub-catalog-dev` for the 24-char limit). Record this in
`docs/progress-log.md` under a new "Messaging Hub — infra naming" heading
before writing Bicep, so later services (Ingestion, Routing, ...) follow the
same pattern with their own service segment.

- [ ] **Step 2: Create the resource group manually (learning step, per project convention)**

Run:
```bash
az group create --name rg-messaginghub-dev --location westeurope
```

- [ ] **Step 3: Write `infra/modules/catalog/keyvault.bicep`**

```bicep
param location string
param keyVaultName string
param principalId string

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    enabledForTemplateDeployment: true
  }
}

resource secretsOfficerRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, principalId, 'KeyVaultSecretsOfficer')
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'b86a8fe4-44ce-4948-aee5-eccb2c155cd7')
    principalId: principalId
    principalType: 'User'
  }
}

output keyVaultName string = keyVault.name
```

- [ ] **Step 4: Write `infra/modules/catalog/sql.bicep`**

```bicep
param location string
param sqlServerName string
param sqlAdminLogin string
@secure()
param sqlAdminPassword string
param databaseName string

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    administratorLogin: sqlAdminLogin
    administratorLoginPassword: sqlAdminPassword
  }
}

resource allowAzureServices 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource database 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: databaseName
  location: location
  sku: {
    name: 'GP_S_Gen5'
    tier: 'GeneralPurpose'
    family: 'Gen5'
    capacity: 1
  }
  properties: {
    useFreeLimit: true
    freeLimitExhaustionBehavior: 'AutoPause'
    autoPauseDelay: 60
  }
}

output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
```

- [ ] **Step 5: Write `infra/main.bicep`**

```bicep
targetScope = 'resourceGroup'

param location string = resourceGroup().location
param sqlAdminLogin string
@secure()
param sqlAdminPassword string
param principalId string

module catalogKeyVault 'modules/catalog/keyvault.bicep' = {
  name: 'catalogKeyVault'
  params: {
    location: location
    keyVaultName: 'kv-msghub-catalog-dev'
    principalId: principalId
  }
}

module catalogSql 'modules/catalog/sql.bicep' = {
  name: 'catalogSql'
  params: {
    location: location
    sqlServerName: 'sql-messaginghub-catalog-dev'
    sqlAdminLogin: sqlAdminLogin
    sqlAdminPassword: sqlAdminPassword
    databaseName: 'catalog'
  }
}

output catalogSqlServerFqdn string = catalogSql.outputs.sqlServerFqdn
output catalogKeyVaultName string = catalogKeyVault.outputs.keyVaultName
```

- [ ] **Step 6: Write `infra/main.dev.bicepparam`**

```bicep
using 'main.bicep'

param sqlAdminLogin = 'sqladmin'
param sqlAdminPassword = az.getSecret('<subscription-id>', 'rg-messaginghub-dev', 'kv-msghub-catalog-dev', 'sql-admin-password')
param principalId = '<your-entra-object-id>'
```

Note: on first deploy, `kv-msghub-catalog-dev` won't have a
`sql-admin-password` secret yet — this is a bootstrap chicken/egg the same
as the superseded build hit. Resolve it by deploying the Key Vault module
alone first (comment out the `catalogSql` module and the
`sqlAdminPassword` param temporarily, or set a placeholder literal password
for the very first `what-if`), then set the real secret via `az keyvault
secret set`, then deploy the full template. Record the exact steps taken in
`docs/progress-log.md` once done, since the exact CLI incantation matters
for next time.

- [ ] **Step 7: Validate with what-if**

Run:
```bash
az deployment group what-if --resource-group rg-messaginghub-dev --template-file infra/main.bicep --parameters infra/main.dev.bicepparam
```
Expected: shows planned creates for the Key Vault, SQL server, firewall rule, and database — no errors.

- [ ] **Step 8: Deploy**

Run:
```bash
az deployment group create --resource-group rg-messaginghub-dev --template-file infra/main.bicep --parameters infra/main.dev.bicepparam
```
Expected: deployment succeeds; note the `catalogSqlServerFqdn` output.

- [ ] **Step 9: Wire the real connection string into Catalog.Api's user secrets**

Run:
```bash
$sqlPassword = az keyvault secret show --vault-name kv-msghub-catalog-dev --name sql-admin-password --query "value" -o tsv
$connString = "Server=tcp:sql-messaginghub-catalog-dev.database.windows.net,1433;Database=catalog;User ID=sqladmin;Password=$sqlPassword;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
cd services/Catalog/Catalog.Api
dotnet user-secrets set "ConnectionStrings:CatalogDb" "$connString"
```

- [ ] **Step 10: Generate and apply the first EF Core migration against the real database**

Run:
```bash
dotnet tool install --global dotnet-ef # if not already installed
cd services/Catalog/Catalog.Infrastructure
dotnet ef migrations add InitialCreate --startup-project ../Catalog.Api --output-dir Persistence/Migrations
dotnet ef database update --startup-project ../Catalog.Api
```
Expected: `MessageTypes` table exists in the `catalog` database on Azure SQL.

- [ ] **Step 11: Manually verify against the real service**

Run:
```bash
cd services/Catalog/Catalog.Api
dotnet run
```
In another terminal, `POST` a message type and `GET` it back to confirm the
API is actually talking to Azure SQL, not the InMemory fallback (remove the
`ConnectionStrings:CatalogDb` fallback check by confirming
`appsettings.Development.json` doesn't also define an empty one that would
shadow the user secret).

- [ ] **Step 12: Update the progress log**

Add a new section to `docs/progress-log.md` documenting: the new resource
group name/naming convention, the Key Vault bootstrap sequence actually
used, and confirmation the Catalog service is live end-to-end against Azure
SQL.

- [ ] **Step 13: Commit**

```bash
git add infra docs/progress-log.md
git commit -m "Add Catalog Bicep infra and deploy to new resource group"
```

---

## Self-Review Notes

- **Spec coverage:** Catalog's responsibility ("registry of message types,
  schemas, versions, routing rules, subscriber registrations") is only
  partially covered — this plan builds message-type registration/lookup
  only. Routing rules and subscriber registrations are deliberately deferred
  to when Routing/Delivery are built and need them, to keep this first plan
  small; noted here rather than silently dropped.
- **Placeholder scan:** none found — every step has runnable commands or
  complete code.
- **Type consistency:** `MessageTypeDto`, `IMessageTypeRepository`,
  `MessageType` signatures are identical across Tasks 3-6.
