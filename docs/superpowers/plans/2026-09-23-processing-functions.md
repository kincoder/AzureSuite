# Processing.Functions Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build `Processing.Functions`, the Service Bus–triggered orchestrator that validates, authorizes, and fans out each inbound message using Catalog's endpoints from the Catalog Client & Route plan, and records the outcome to Cosmos DB.

**Architecture:** New `services/Processing/` solution folder (`Processing.Application`, `Processing.Infrastructure`, `Processing.Functions`, no `Domain` project — Processing owns no data of its own, matching Ingestion's original "no Domain until there's an actual entity" decision). A small `Ingestion.Api` contract change adds `ClientId`, which Processing needs to call Catalog's route-lookup endpoint.

**Tech Stack:** Azure Functions isolated worker (.NET 10), `Azure.Messaging.ServiceBus`, `Microsoft.Azure.Cosmos`, MediatR (same command-handler shape as Catalog/Ingestion, invoked directly by the Function rather than through ASP.NET Core), xUnit + FluentAssertions.

**Spec:** `docs/superpowers/specs/2026-09-23-routing-processing-design.md` (Processing.Functions section)

**Depends on:** `docs/superpowers/plans/2026-09-23-catalog-client-route.md` — Catalog's `/message-types/{name}/{version}/validate` and `/routes/lookup` endpoints must exist and be deployed (or runnable locally) before Task 4 of this plan can be exercised end-to-end. Tasks 1–3, 5–7 have no such dependency and can be built first if useful.

## Global Constraints

- Warning-free build, `Nullable` enabled, no `#pragma warning disable` except EF migration files (not applicable to this plan — Processing has no database).
- 1:1 `tests/X.Y.Tests` project per source project, mirroring folder structure.
- xUnit + FluentAssertions; hand-written fakes before reaching for a mocking library.
- Processing owns no persistent state of its own — every abstraction it depends on (`ICatalogClient`, `ILifecycleEventStore`, `IFanOutPublisher`) is an interface with a real implementation in `Processing.Infrastructure`, never a direct SDK call from the command handler.
- Fan-out is explicit, in code — one Service Bus send per target queue name, never Service Bus topics/subscriptions.
- Any unhandled exception in the Function must NOT be caught and swallowed — let it propagate so the Service Bus trigger leaves the message uncompleted and Service Bus's own lock/redelivery retries it.

---

## File Structure

```
services/Ingestion/
  Ingestion.Api/Contracts/IngestMessageRequest.cs         (modify: add ClientId)
  Ingestion.Application/Messages/Commands/IngestMessage/
    IngestMessageCommand.cs                                (modify: add ClientId)
    IngestMessageCommandHandler.cs                          (modify: pass ClientId through)
  Ingestion.Application/Abstractions/IMessagePublisher.cs   (modify: add clientId parameter)
  Ingestion.Infrastructure/Messaging/ServiceBusMessagePublisher.cs   (modify: stamp clientId)

services/Processing/
  Processing.Application/
    Abstractions/
      ICatalogClient.cs                                     (new)
      ILifecycleEventStore.cs                                (new)
      IFanOutPublisher.cs                                     (new)
    Messages/
      ValidationResult.cs, RouteLookupResult.cs               (new)
      Commands/ProcessInboundMessage/
        ProcessInboundMessageCommand.cs
        ProcessInboundMessageCommandHandler.cs
        ProcessingOutcomeDto.cs                                (new)
  Processing.Infrastructure/
    Catalog/CatalogHttpClient.cs                              (new)
    Messaging/ServiceBusFanOutPublisher.cs                    (new)
    Persistence/{LifecycleEvent,CosmosLifecycleEventStore}.cs  (new)
  Processing.Functions/
    Program.cs                                                (new)
    ProcessInboundMessageFunction.cs                           (new)
    host.json, local.settings.json                             (new)

tests/
  Ingestion.Application.Tests/Messages/Commands/IngestMessage/IngestMessageCommandHandlerTests.cs   (modify)
  Processing.Application.Tests/
    TestDoubles/{FakeCatalogClient,FakeLifecycleEventStore,FakeFanOutPublisher}.cs   (new)
    Messages/Commands/ProcessInboundMessage/ProcessInboundMessageCommandHandlerTests.cs   (new)

infra/modules/processing/
  functionapp.bicep, storage.bicep, servicebus-output.bicep   (new)
infra/modules/shared/
  cosmos.bicep                                                (new)
infra/main.bicep                                               (modify)
.github/workflows/build.yml                                    (modify)

AzureSuite.slnx                                                 (modify: add Processing projects)
```

---

### Task 1: Add `ClientId` to Ingestion's submission contract

**Files:**
- Modify: `services/Ingestion/Ingestion.Api/Contracts/IngestMessageRequest.cs`
- Modify: `services/Ingestion/Ingestion.Application/Messages/Commands/IngestMessage/IngestMessageCommand.cs`
- Modify: `services/Ingestion/Ingestion.Application/Messages/Commands/IngestMessage/IngestMessageCommandHandler.cs`
- Modify: `services/Ingestion/Ingestion.Application/Abstractions/IMessagePublisher.cs`
- Modify: `services/Ingestion/Ingestion.Infrastructure/Messaging/ServiceBusMessagePublisher.cs`
- Modify: `services/Ingestion/Ingestion.Api/Program.cs`
- Test: `tests/Ingestion.Application.Tests/Messages/Commands/IngestMessage/IngestMessageCommandHandlerTests.cs`

**Interfaces:**
- Produces: `IMessagePublisher.PublishAsync(Guid messageId, Guid clientId, string messageType, string version, string payload, CancellationToken)` — the `clientId` parameter Processing's Task 4/7 will read back off the Service Bus message.

- [ ] **Step 1: Update the failing/changed test**

```csharp
// tests/Ingestion.Application.Tests/Messages/Commands/IngestMessage/IngestMessageCommandHandlerTests.cs
// Update the existing test(s) to include a ClientId argument, e.g.:
var command = new IngestMessageCommand(Guid.NewGuid(), "pacs.008", "1.0", "{}");
var result = await handler.Handle(command, CancellationToken.None);
// ...and update the FakePublisher test double's PublishAsync signature to match.
```

Read the existing test file and existing `FakePublisher`/`FakeMessagePublisher` test double first (`tests/Ingestion.Application.Tests/TestDoubles/`) and update both to the new five-parameter `PublishAsync` signature — this is a mechanical signature change, not new test scenarios.

- [ ] **Step 2: Run tests to verify they fail to compile**

Run: `dotnet build tests/Ingestion.Application.Tests`
Expected: FAIL — signature mismatch.

- [ ] **Step 3: Update the contract, command, and handler**

```csharp
// services/Ingestion/Ingestion.Api/Contracts/IngestMessageRequest.cs
namespace AzureSuite.Ingestion.Api.Contracts
{
    /// <summary>Request body for <c>POST /messages</c>. All four fields are required.</summary>
    public record IngestMessageRequest(Guid ClientId, string MessageType, string Version, string Payload);
}
```

```csharp
// services/Ingestion/Ingestion.Application/Messages/Commands/IngestMessage/IngestMessageCommand.cs
using AzureSuite.Ingestion.Application.Messages;
using MediatR;

namespace AzureSuite.Ingestion.Application.Messages.Commands.IngestMessage
{
    public record IngestMessageCommand(Guid ClientId, string MessageType, string Version, string Payload) : IRequest<IngestedMessageDto>;
}
```

```csharp
// services/Ingestion/Ingestion.Application/Abstractions/IMessagePublisher.cs
namespace AzureSuite.Ingestion.Application.Abstractions
{
    public interface IMessagePublisher
    {
        Task PublishAsync(Guid messageId, Guid clientId, string messageType, string version, string payload, CancellationToken cancellationToken);
    }
}
```

```csharp
// services/Ingestion/Ingestion.Application/Messages/Commands/IngestMessage/IngestMessageCommandHandler.cs
// Change the PublishAsync call site to:
await _publisher.PublishAsync(messageId, request.ClientId, request.MessageType, request.Version, request.Payload, cancellationToken);
```

```csharp
// services/Ingestion/Ingestion.Infrastructure/Messaging/ServiceBusMessagePublisher.cs
public async Task PublishAsync(Guid messageId, Guid clientId, string messageType, string version, string payload, CancellationToken cancellationToken)
{
    var message = new ServiceBusMessage(payload)
    {
        MessageId = messageId.ToString()
    };
    message.ApplicationProperties["clientId"] = clientId.ToString();
    message.ApplicationProperties["messageType"] = messageType;
    message.ApplicationProperties["version"] = version;

    await _sender.SendMessageAsync(message, cancellationToken);
}
```

```csharp
// services/Ingestion/Ingestion.Api/Program.cs
// Update the /messages endpoint's mediator.Send call to pass request.ClientId:
var dto = await mediator.Send(new IngestMessageCommand(request.ClientId, request.MessageType, request.Version, request.Payload));
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Ingestion.Application.Tests`
Expected: PASS, no warnings.

- [ ] **Step 5: Commit**

```bash
git add services/Ingestion tests/Ingestion.Application.Tests
git commit -m "feat(ingestion): add ClientId to submission contract"
```

---

### Task 2: `Processing.Application` scaffolding and abstractions

**Files:**
- Create: `services/Processing/Processing.Application/Processing.Application.csproj`
- Create: `services/Processing/Processing.Application/Abstractions/{ICatalogClient,ILifecycleEventStore,IFanOutPublisher}.cs`
- Create: `services/Processing/Processing.Application/Messages/{ValidationResult,RouteLookupResult}.cs`
- Modify: `AzureSuite.slnx`

**Interfaces:**
- Produces: `ICatalogClient.ValidateAsync(string messageType, string version, string payload, CancellationToken) : Task<ValidationResult>`; `ICatalogClient.LookupRouteAsync(Guid clientId, string messageType, string version, CancellationToken) : Task<RouteLookupResult>`; `ILifecycleEventStore.RecordAsync(LifecycleEvent, CancellationToken) : Task` (the `LifecycleEvent` type itself is defined in Task 6, alongside its store implementation, since only the store needs its shape until then — `ILifecycleEventStore` here references it by forward declaration in the same Processing.Application project, defined as a plain record in this task instead, see Step 1); `IFanOutPublisher.PublishAsync(IReadOnlyList<string> queueNames, string payload, CancellationToken) : Task`.

- [ ] **Step 1: Create the project**

```bash
dotnet new classlib -n Processing.Application -o services/Processing/Processing.Application --framework net10.0
```

Edit the generated `.csproj` to match `Catalog.Application`'s shape (`Nullable` enabled, `ImplicitUsings` enabled, `MediatR` package reference):

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="MediatR" Version="14.2.0" />
  </ItemGroup>
</Project>
```

Add it to `AzureSuite.slnx` under a new `/services/Processing/` folder, matching the `/services/Ingestion/` folder's shape.

- [ ] **Step 2: Define the shared message-shape records**

```csharp
// services/Processing/Processing.Application/Messages/ValidationResult.cs
namespace AzureSuite.Processing.Application.Messages
{
    public record ValidationResult(bool IsValid, IReadOnlyList<string> Errors);
}
```

```csharp
// services/Processing/Processing.Application/Messages/RouteLookupResult.cs
namespace AzureSuite.Processing.Application.Messages
{
    public record RouteLookupResult(IReadOnlyList<string> QueueNames);
}
```

```csharp
// services/Processing/Processing.Application/Messages/LifecycleEvent.cs
namespace AzureSuite.Processing.Application.Messages
{
    /// <summary>One row written to Cosmos per processed message. Status is "Rejected" (Reason
    /// set, QueueNames null) or "Routed" (QueueNames set, Reason null).</summary>
    public record LifecycleEvent(Guid MessageId, string MessageType, string Version, Guid ClientId, string Status, string? Reason, IReadOnlyList<string>? QueueNames, DateTime TimestampUtc);
}
```

- [ ] **Step 3: Define the abstractions**

```csharp
// services/Processing/Processing.Application/Abstractions/ICatalogClient.cs
using AzureSuite.Processing.Application.Messages;

namespace AzureSuite.Processing.Application.Abstractions
{
    /// <summary>Calls Catalog's two single-purpose endpoints. Deliberately two methods, not
    /// one combined call — see the spec for why validation and authorization stay separate.</summary>
    public interface ICatalogClient
    {
        Task<ValidationResult> ValidateAsync(string messageType, string version, string payload, CancellationToken cancellationToken);
        Task<RouteLookupResult> LookupRouteAsync(Guid clientId, string messageType, string version, CancellationToken cancellationToken);
    }
}
```

```csharp
// services/Processing/Processing.Application/Abstractions/ILifecycleEventStore.cs
using AzureSuite.Processing.Application.Messages;

namespace AzureSuite.Processing.Application.Abstractions
{
    public interface ILifecycleEventStore
    {
        Task RecordAsync(LifecycleEvent lifecycleEvent, CancellationToken cancellationToken);
    }
}
```

```csharp
// services/Processing/Processing.Application/Abstractions/IFanOutPublisher.cs
namespace AzureSuite.Processing.Application.Abstractions
{
    /// <summary>Publishes one copy of Payload to each of QueueNames — the explicit, in-code
    /// fan-out this design uses instead of Service Bus topics/subscriptions.</summary>
    public interface IFanOutPublisher
    {
        Task PublishAsync(IReadOnlyList<string> queueNames, string payload, CancellationToken cancellationToken);
    }
}
```

- [ ] **Step 4: Build to confirm it compiles**

Run: `dotnet build services/Processing/Processing.Application`
Expected: builds with zero warnings.

- [ ] **Step 5: Commit**

```bash
git add services/Processing/Processing.Application AzureSuite.slnx
git commit -m "feat(processing): scaffold Processing.Application with core abstractions"
```

---

### Task 3: `ProcessInboundMessageCommand` and handler (core orchestration)

**Files:**
- Create: `services/Processing/Processing.Application/Messages/Commands/ProcessInboundMessage/{ProcessInboundMessageCommand,ProcessInboundMessageCommandHandler,ProcessingOutcomeDto}.cs`
- Create: `tests/Processing.Application.Tests/Processing.Application.Tests.csproj`
- Create: `tests/Processing.Application.Tests/TestDoubles/{FakeCatalogClient,FakeLifecycleEventStore,FakeFanOutPublisher}.cs`
- Test: `tests/Processing.Application.Tests/Messages/Commands/ProcessInboundMessage/ProcessInboundMessageCommandHandlerTests.cs`
- Modify: `AzureSuite.slnx`

**Interfaces:**
- Consumes: `ICatalogClient`, `ILifecycleEventStore`, `IFanOutPublisher` from Task 2.
- Produces: `ProcessInboundMessageCommand(Guid MessageId, Guid ClientId, string MessageType, string Version, string Payload) : IRequest<ProcessingOutcomeDto>`; `ProcessingOutcomeDto(bool WasRouted, IReadOnlyList<string> QueueNames, string? RejectionReason)` — consumed by Task 7's Function.

- [ ] **Step 1: Create the test project**

```bash
dotnet new xunit -n Processing.Application.Tests -o tests/Processing.Application.Tests --framework net10.0
dotnet add tests/Processing.Application.Tests package FluentAssertions --version 8.10.0
dotnet add tests/Processing.Application.Tests reference services/Processing/Processing.Application
```

Add it to `AzureSuite.slnx` under `/services/Processing/`.

- [ ] **Step 2: Write the failing tests**

```csharp
// tests/Processing.Application.Tests/TestDoubles/FakeCatalogClient.cs
using AzureSuite.Processing.Application.Abstractions;
using AzureSuite.Processing.Application.Messages;

namespace Processing.Application.Tests.TestDoubles
{
    public class FakeCatalogClient : ICatalogClient
    {
        public ValidationResult ValidationToReturn = new(true, Array.Empty<string>());
        public RouteLookupResult RouteLookupToReturn = new(Array.Empty<string>());

        public Task<ValidationResult> ValidateAsync(string messageType, string version, string payload, CancellationToken cancellationToken)
            => Task.FromResult(ValidationToReturn);

        public Task<RouteLookupResult> LookupRouteAsync(Guid clientId, string messageType, string version, CancellationToken cancellationToken)
            => Task.FromResult(RouteLookupToReturn);
    }
}
```

```csharp
// tests/Processing.Application.Tests/TestDoubles/FakeLifecycleEventStore.cs
using AzureSuite.Processing.Application.Abstractions;
using AzureSuite.Processing.Application.Messages;

namespace Processing.Application.Tests.TestDoubles
{
    public class FakeLifecycleEventStore : ILifecycleEventStore
    {
        public readonly List<LifecycleEvent> RecordedEvents = new();

        public Task RecordAsync(LifecycleEvent lifecycleEvent, CancellationToken cancellationToken)
        {
            RecordedEvents.Add(lifecycleEvent);
            return Task.CompletedTask;
        }
    }
}
```

```csharp
// tests/Processing.Application.Tests/TestDoubles/FakeFanOutPublisher.cs
using AzureSuite.Processing.Application.Abstractions;

namespace Processing.Application.Tests.TestDoubles
{
    public class FakeFanOutPublisher : IFanOutPublisher
    {
        public readonly List<(IReadOnlyList<string> QueueNames, string Payload)> Publishes = new();

        public Task PublishAsync(IReadOnlyList<string> queueNames, string payload, CancellationToken cancellationToken)
        {
            Publishes.Add((queueNames, payload));
            return Task.CompletedTask;
        }
    }
}
```

```csharp
// tests/Processing.Application.Tests/Messages/Commands/ProcessInboundMessage/ProcessInboundMessageCommandHandlerTests.cs
using AzureSuite.Processing.Application.Messages;
using AzureSuite.Processing.Application.Messages.Commands.ProcessInboundMessage;
using FluentAssertions;
using Processing.Application.Tests.TestDoubles;
using Xunit;

namespace Processing.Application.Tests.Messages.Commands.ProcessInboundMessage
{
    public class ProcessInboundMessageCommandHandlerTests
    {
        private static ProcessInboundMessageCommand NewCommand(Guid? clientId = null) =>
            new(Guid.NewGuid(), clientId ?? Guid.NewGuid(), "pacs.008", "1.0", "{ \"amount\": 100 }");

        [Fact]
        public async Task Handle_WithInvalidPayload_RecordsRejectedAndDoesNotPublish()
        {
            var catalogClient = new FakeCatalogClient { ValidationToReturn = new ValidationResult(false, new[] { "amount is required" }) };
            var lifecycleStore = new FakeLifecycleEventStore();
            var fanOutPublisher = new FakeFanOutPublisher();
            var handler = new ProcessInboundMessageCommandHandler(catalogClient, lifecycleStore, fanOutPublisher);

            var result = await handler.Handle(NewCommand(), CancellationToken.None);

            result.WasRouted.Should().BeFalse();
            result.RejectionReason.Should().Contain("amount is required");
            fanOutPublisher.Publishes.Should().BeEmpty();
            lifecycleStore.RecordedEvents.Should().ContainSingle(e => e.Status == "Rejected");
        }

        [Fact]
        public async Task Handle_WithValidPayloadButNoRoute_RecordsRejectedAsUnauthorizedAndDoesNotPublish()
        {
            var catalogClient = new FakeCatalogClient { RouteLookupToReturn = new RouteLookupResult(Array.Empty<string>()) };
            var lifecycleStore = new FakeLifecycleEventStore();
            var fanOutPublisher = new FakeFanOutPublisher();
            var handler = new ProcessInboundMessageCommandHandler(catalogClient, lifecycleStore, fanOutPublisher);

            var result = await handler.Handle(NewCommand(), CancellationToken.None);

            result.WasRouted.Should().BeFalse();
            result.RejectionReason.Should().Contain("not authorized");
            fanOutPublisher.Publishes.Should().BeEmpty();
            lifecycleStore.RecordedEvents.Should().ContainSingle(e => e.Status == "Rejected");
        }

        [Fact]
        public async Task Handle_WithValidPayloadAndSingleQueueRoute_PublishesAndRecordsRouted()
        {
            var catalogClient = new FakeCatalogClient { RouteLookupToReturn = new RouteLookupResult(new[] { "out-settlements" }) };
            var lifecycleStore = new FakeLifecycleEventStore();
            var fanOutPublisher = new FakeFanOutPublisher();
            var handler = new ProcessInboundMessageCommandHandler(catalogClient, lifecycleStore, fanOutPublisher);

            var result = await handler.Handle(NewCommand(), CancellationToken.None);

            result.WasRouted.Should().BeTrue();
            result.QueueNames.Should().Equal("out-settlements");
            fanOutPublisher.Publishes.Should().ContainSingle();
            fanOutPublisher.Publishes[0].QueueNames.Should().Equal("out-settlements");
            lifecycleStore.RecordedEvents.Should().ContainSingle(e => e.Status == "Routed");
        }

        [Fact]
        public async Task Handle_WithMultiQueueRoute_PublishesToAllQueuesInOneFanOutCall()
        {
            var catalogClient = new FakeCatalogClient { RouteLookupToReturn = new RouteLookupResult(new[] { "out-notifications", "out-audit" }) };
            var lifecycleStore = new FakeLifecycleEventStore();
            var fanOutPublisher = new FakeFanOutPublisher();
            var handler = new ProcessInboundMessageCommandHandler(catalogClient, lifecycleStore, fanOutPublisher);

            var result = await handler.Handle(NewCommand(), CancellationToken.None);

            result.QueueNames.Should().BeEquivalentTo(new[] { "out-notifications", "out-audit" });
            fanOutPublisher.Publishes.Should().ContainSingle();
            fanOutPublisher.Publishes[0].QueueNames.Should().BeEquivalentTo(new[] { "out-notifications", "out-audit" });
        }
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test tests/Processing.Application.Tests`
Expected: FAIL — `ProcessInboundMessageCommand`/`ProcessInboundMessageCommandHandler` don't exist.

- [ ] **Step 4: Write minimal implementation**

```csharp
// services/Processing/Processing.Application/Messages/Commands/ProcessInboundMessage/ProcessingOutcomeDto.cs
namespace AzureSuite.Processing.Application.Messages.Commands.ProcessInboundMessage
{
    public record ProcessingOutcomeDto(bool WasRouted, IReadOnlyList<string> QueueNames, string? RejectionReason);
}
```

```csharp
// services/Processing/Processing.Application/Messages/Commands/ProcessInboundMessage/ProcessInboundMessageCommand.cs
using MediatR;

namespace AzureSuite.Processing.Application.Messages.Commands.ProcessInboundMessage
{
    public record ProcessInboundMessageCommand(Guid MessageId, Guid ClientId, string MessageType, string Version, string Payload) : IRequest<ProcessingOutcomeDto>;
}
```

```csharp
// services/Processing/Processing.Application/Messages/Commands/ProcessInboundMessage/ProcessInboundMessageCommandHandler.cs
using AzureSuite.Processing.Application.Abstractions;
using MediatR;

namespace AzureSuite.Processing.Application.Messages.Commands.ProcessInboundMessage
{
    public class ProcessInboundMessageCommandHandler : IRequestHandler<ProcessInboundMessageCommand, ProcessingOutcomeDto>
    {
        private readonly ICatalogClient _catalogClient;
        private readonly ILifecycleEventStore _lifecycleEventStore;
        private readonly IFanOutPublisher _fanOutPublisher;

        public ProcessInboundMessageCommandHandler(ICatalogClient catalogClient, ILifecycleEventStore lifecycleEventStore, IFanOutPublisher fanOutPublisher)
        {
            _catalogClient = catalogClient;
            _lifecycleEventStore = lifecycleEventStore;
            _fanOutPublisher = fanOutPublisher;
        }

        public async Task<ProcessingOutcomeDto> Handle(ProcessInboundMessageCommand request, CancellationToken cancellationToken)
        {
            var validation = await _catalogClient.ValidateAsync(request.MessageType, request.Version, request.Payload, cancellationToken);
            if (!validation.IsValid)
            {
                var reason = string.Join("; ", validation.Errors);
                await RecordRejectionAsync(request, reason, cancellationToken);
                return new ProcessingOutcomeDto(false, Array.Empty<string>(), reason);
            }

            var routeLookup = await _catalogClient.LookupRouteAsync(request.ClientId, request.MessageType, request.Version, cancellationToken);
            if (routeLookup.QueueNames.Count == 0)
            {
                const string reason = "Client is not authorized to send this message type.";
                await RecordRejectionAsync(request, reason, cancellationToken);
                return new ProcessingOutcomeDto(false, Array.Empty<string>(), reason);
            }

            await _fanOutPublisher.PublishAsync(routeLookup.QueueNames, request.Payload, cancellationToken);

            await _lifecycleEventStore.RecordAsync(
                new LifecycleEvent(request.MessageId, request.MessageType, request.Version, request.ClientId, "Routed", null, routeLookup.QueueNames, DateTime.UtcNow),
                cancellationToken);

            return new ProcessingOutcomeDto(true, routeLookup.QueueNames, null);
        }

        private Task RecordRejectionAsync(ProcessInboundMessageCommand request, string reason, CancellationToken cancellationToken)
        {
            return _lifecycleEventStore.RecordAsync(
                new LifecycleEvent(request.MessageId, request.MessageType, request.Version, request.ClientId, "Rejected", reason, null, DateTime.UtcNow),
                cancellationToken);
        }
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/Processing.Application.Tests`
Expected: PASS, no warnings.

- [ ] **Step 6: Commit**

```bash
git add services/Processing/Processing.Application/Messages tests/Processing.Application.Tests AzureSuite.slnx
git commit -m "feat(processing): add ProcessInboundMessage orchestration"
```

---

### Task 4: `CatalogHttpClient`

**Files:**
- Create: `services/Processing/Processing.Infrastructure/Processing.Infrastructure.csproj`
- Create: `services/Processing/Processing.Infrastructure/Catalog/CatalogHttpClient.cs`
- Create: `tests/Processing.Infrastructure.Tests/Processing.Infrastructure.Tests.csproj`
- Test: `tests/Processing.Infrastructure.Tests/Catalog/CatalogHttpClientTests.cs`
- Modify: `AzureSuite.slnx`

**Interfaces:**
- Consumes: `Catalog.Api`'s `POST /message-types/{name}/{version}/validate` and `GET /routes/lookup` endpoints (from the Catalog Client & Route plan).
- Produces: `CatalogHttpClient : ICatalogClient` (constructor takes `HttpClient`).

- [ ] **Step 1: Create the project**

```bash
dotnet new classlib -n Processing.Infrastructure -o services/Processing/Processing.Infrastructure --framework net10.0
dotnet add services/Processing/Processing.Infrastructure reference services/Processing/Processing.Application
```

Set `Nullable`/`ImplicitUsings` to `enable` in the generated `.csproj`, matching Task 2's project. Add to `AzureSuite.slnx`.

- [ ] **Step 2: Write the failing test**

Uses `HttpClient` with a fake `HttpMessageHandler` — no real network call, matching how this codebase avoids a mocking library elsewhere.

```csharp
// tests/Processing.Infrastructure.Tests/Catalog/CatalogHttpClientTests.cs
using System.Net;
using System.Text;
using System.Text.Json;
using AzureSuite.Processing.Infrastructure.Catalog;
using FluentAssertions;
using Xunit;

namespace Processing.Infrastructure.Tests.Catalog
{
    public class CatalogHttpClientTests
    {
        private class StubHandler : HttpMessageHandler
        {
            private readonly string _responseJson;
            public HttpRequestMessage? LastRequest;

            public StubHandler(string responseJson) => _responseJson = responseJson;

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                LastRequest = request;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(_responseJson, Encoding.UTF8, "application/json")
                });
            }
        }

        [Fact]
        public async Task ValidateAsync_ParsesValidationResult()
        {
            var handler = new StubHandler(JsonSerializer.Serialize(new { isValid = true, errors = Array.Empty<string>() }));
            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://catalog.local/") };
            var client = new CatalogHttpClient(httpClient);

            var result = await client.ValidateAsync("pacs.008", "1.0", "{}", CancellationToken.None);

            result.IsValid.Should().BeTrue();
            handler.LastRequest!.RequestUri!.PathAndQuery.Should().Be("/message-types/pacs.008/1.0/validate");
        }

        [Fact]
        public async Task LookupRouteAsync_ParsesQueueNames()
        {
            var handler = new StubHandler(JsonSerializer.Serialize(new { queueNames = new[] { "out-settlements" } }));
            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://catalog.local/") };
            var client = new CatalogHttpClient(httpClient);
            var clientId = Guid.NewGuid();

            var result = await client.LookupRouteAsync(clientId, "pacs.008", "1.0", CancellationToken.None);

            result.QueueNames.Should().Equal("out-settlements");
            handler.LastRequest!.RequestUri!.PathAndQuery.Should().Be($"/routes/lookup?clientId={clientId}&messageType=pacs.008&version=1.0");
        }
    }
}
```

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test tests/Processing.Infrastructure.Tests`
Expected: FAIL — `CatalogHttpClient` does not exist.

- [ ] **Step 4: Write minimal implementation**

```csharp
// services/Processing/Processing.Infrastructure/Catalog/CatalogHttpClient.cs
using System.Net.Http.Json;
using AzureSuite.Processing.Application.Abstractions;
using AzureSuite.Processing.Application.Messages;

namespace AzureSuite.Processing.Infrastructure.Catalog
{
    public class CatalogHttpClient : ICatalogClient
    {
        private record ValidateRequestBody(string Payload);
        private record ValidateResponseBody(bool IsValid, IReadOnlyList<string> Errors);
        private record RouteLookupResponseBody(IReadOnlyList<string> QueueNames);

        private readonly HttpClient _httpClient;

        public CatalogHttpClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<ValidationResult> ValidateAsync(string messageType, string version, string payload, CancellationToken cancellationToken)
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"/message-types/{messageType}/{version}/validate",
                new ValidateRequestBody(payload),
                cancellationToken);
            response.EnsureSuccessStatusCode();

            var body = (await response.Content.ReadFromJsonAsync<ValidateResponseBody>(cancellationToken))!;
            return new ValidationResult(body.IsValid, body.Errors);
        }

        public async Task<RouteLookupResult> LookupRouteAsync(Guid clientId, string messageType, string version, CancellationToken cancellationToken)
        {
            var response = await _httpClient.GetAsync(
                $"/routes/lookup?clientId={clientId}&messageType={messageType}&version={version}",
                cancellationToken);
            response.EnsureSuccessStatusCode();

            var body = (await response.Content.ReadFromJsonAsync<RouteLookupResponseBody>(cancellationToken))!;
            return new RouteLookupResult(body.QueueNames);
        }
    }
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test tests/Processing.Infrastructure.Tests`
Expected: PASS, no warnings.

- [ ] **Step 6: Commit**

```bash
git add services/Processing/Processing.Infrastructure tests/Processing.Infrastructure.Tests AzureSuite.slnx
git commit -m "feat(processing): add CatalogHttpClient"
```

---

### Task 5: `ServiceBusFanOutPublisher`

**Files:**
- Create: `services/Processing/Processing.Infrastructure/Messaging/ServiceBusFanOutPublisher.cs`
- Test: `tests/Processing.Infrastructure.Tests/Messaging/ServiceBusFanOutPublisherTests.cs`
- Modify: `services/Processing/Processing.Infrastructure/Processing.Infrastructure.csproj` (add `Azure.Messaging.ServiceBus`)

**Interfaces:**
- Produces: `ServiceBusFanOutPublisher : IFanOutPublisher` (constructor takes `ServiceBusClient`).

- [ ] **Step 1: Add the package**

```bash
dotnet add services/Processing/Processing.Infrastructure package Azure.Messaging.ServiceBus
```

- [ ] **Step 2: Write the test**

Matches the existing `ServiceBusMessagePublisherTests`' scope in Ingestion — verifies the sends are constructed correctly, not a live Service Bus round-trip (no local emulator wired up, same limitation Ingestion's design already accepted).

```csharp
// tests/Processing.Infrastructure.Tests/Messaging/ServiceBusFanOutPublisherTests.cs
using AzureSuite.Processing.Infrastructure.Messaging;
using FluentAssertions;
using Xunit;

namespace Processing.Infrastructure.Tests.Messaging
{
    public class ServiceBusFanOutPublisherTests
    {
        [Fact]
        public void Constructor_WithServiceBusClient_DoesNotThrow()
        {
            // A full send-path test needs a live or emulated Service Bus, not available in
            // this increment (matching Ingestion.Infrastructure's existing limitation).
            // This test only confirms the type constructs correctly via DI.
            var act = () => new ServiceBusFanOutPublisher(new Azure.Messaging.ServiceBus.ServiceBusClient("Endpoint=sb://localhost/;SharedAccessKeyName=x;SharedAccessKey=eA=="));

            act.Should().NotThrow();
        }
    }
}
```

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test tests/Processing.Infrastructure.Tests --filter ServiceBusFanOutPublisherTests`
Expected: FAIL — type doesn't exist.

- [ ] **Step 4: Write minimal implementation**

```csharp
// services/Processing/Processing.Infrastructure/Messaging/ServiceBusFanOutPublisher.cs
using Azure.Messaging.ServiceBus;
using AzureSuite.Processing.Application.Abstractions;

namespace AzureSuite.Processing.Infrastructure.Messaging
{
    /// <summary>Sends one copy of the payload to each target queue, explicitly in code —
    /// never via Service Bus topics/subscriptions. One ServiceBusSender per queue name,
    /// created on demand and cached for the client's lifetime.</summary>
    public class ServiceBusFanOutPublisher : IFanOutPublisher, IAsyncDisposable
    {
        private readonly ServiceBusClient _client;
        private readonly Dictionary<string, ServiceBusSender> _senders = new();

        public ServiceBusFanOutPublisher(ServiceBusClient client)
        {
            _client = client;
        }

        public async Task PublishAsync(IReadOnlyList<string> queueNames, string payload, CancellationToken cancellationToken)
        {
            foreach (var queueName in queueNames)
            {
                if (!_senders.TryGetValue(queueName, out var sender))
                {
                    sender = _client.CreateSender(queueName);
                    _senders[queueName] = sender;
                }

                await sender.SendMessageAsync(new ServiceBusMessage(payload), cancellationToken);
            }
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var sender in _senders.Values)
            {
                await sender.DisposeAsync();
            }
        }
    }
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test tests/Processing.Infrastructure.Tests --filter ServiceBusFanOutPublisherTests`
Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add services/Processing/Processing.Infrastructure/Messaging services/Processing/Processing.Infrastructure/Processing.Infrastructure.csproj tests/Processing.Infrastructure.Tests/Messaging
git commit -m "feat(processing): add ServiceBusFanOutPublisher"
```

---

### Task 6: `CosmosLifecycleEventStore`

**Files:**
- Create: `services/Processing/Processing.Infrastructure/Persistence/CosmosLifecycleEventStore.cs`
- Test: `tests/Processing.Infrastructure.Tests/Persistence/CosmosLifecycleEventStoreTests.cs`
- Modify: `services/Processing/Processing.Infrastructure/Processing.Infrastructure.csproj` (add `Microsoft.Azure.Cosmos`)

**Interfaces:**
- Produces: `CosmosLifecycleEventStore : ILifecycleEventStore` (constructor takes `Container`).

- [ ] **Step 1: Add the package**

```bash
dotnet add services/Processing/Processing.Infrastructure package Microsoft.Azure.Cosmos
```

- [ ] **Step 2: Write the test**

The Cosmos SDK's `Container` is not easily fakeable without the Cosmos Emulator (not wired up this increment, same "no live-service test" pattern as Task 5). This test instead verifies the document shape the store builds, via a small seam that separates "build the document" from "call Cosmos":

```csharp
// tests/Processing.Infrastructure.Tests/Persistence/CosmosLifecycleEventStoreTests.cs
using AzureSuite.Processing.Application.Messages;
using AzureSuite.Processing.Infrastructure.Persistence;
using FluentAssertions;
using Xunit;

namespace Processing.Infrastructure.Tests.Persistence
{
    public class CosmosLifecycleEventStoreTests
    {
        [Fact]
        public void ToDocument_MapsAllFieldsAndUsesMessageIdAsId()
        {
            var lifecycleEvent = new LifecycleEvent(Guid.NewGuid(), "pacs.008", "1.0", Guid.NewGuid(), "Routed", null, new[] { "out-settlements" }, DateTime.UtcNow);

            var document = CosmosLifecycleEventStore.ToDocument(lifecycleEvent);

            document.id.Should().Be(lifecycleEvent.MessageId.ToString());
            document.messageId.Should().Be(lifecycleEvent.MessageId.ToString());
            document.status.Should().Be("Routed");
            document.queueNames.Should().Equal("out-settlements");
        }
    }
}
```

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test tests/Processing.Infrastructure.Tests --filter CosmosLifecycleEventStoreTests`
Expected: FAIL — type doesn't exist.

- [ ] **Step 4: Write minimal implementation**

```csharp
// services/Processing/Processing.Infrastructure/Persistence/CosmosLifecycleEventStore.cs
using AzureSuite.Processing.Application.Abstractions;
using AzureSuite.Processing.Application.Messages;
using Microsoft.Azure.Cosmos;

namespace AzureSuite.Processing.Infrastructure.Persistence
{
    public class CosmosLifecycleEventStore : ILifecycleEventStore
    {
        // Partition key is /messageId (see infra/modules/shared/cosmos.bicep, Task 8).
        public record LifecycleEventDocument(string id, string messageId, string messageType, string version, string clientId, string status, string? reason, IReadOnlyList<string>? queueNames, DateTime timestampUtc);

        private readonly Container _container;

        public CosmosLifecycleEventStore(Container container)
        {
            _container = container;
        }

        public static LifecycleEventDocument ToDocument(LifecycleEvent lifecycleEvent)
        {
            return new LifecycleEventDocument(
                id: lifecycleEvent.MessageId.ToString(),
                messageId: lifecycleEvent.MessageId.ToString(),
                messageType: lifecycleEvent.MessageType,
                version: lifecycleEvent.Version,
                clientId: lifecycleEvent.ClientId.ToString(),
                status: lifecycleEvent.Status,
                reason: lifecycleEvent.Reason,
                queueNames: lifecycleEvent.QueueNames,
                timestampUtc: lifecycleEvent.TimestampUtc);
        }

        public async Task RecordAsync(LifecycleEvent lifecycleEvent, CancellationToken cancellationToken)
        {
            var document = ToDocument(lifecycleEvent);
            await _container.CreateItemAsync(document, new PartitionKey(document.messageId), cancellationToken: cancellationToken);
        }
    }
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test tests/Processing.Infrastructure.Tests --filter CosmosLifecycleEventStoreTests`
Expected: PASS

- [ ] **Step 6: Run the full Processing.Infrastructure suite**

Run: `dotnet test tests/Processing.Infrastructure.Tests`
Expected: PASS, no warnings.

- [ ] **Step 7: Commit**

```bash
git add services/Processing/Processing.Infrastructure/Persistence services/Processing/Processing.Infrastructure/Processing.Infrastructure.csproj tests/Processing.Infrastructure.Tests/Persistence
git commit -m "feat(processing): add CosmosLifecycleEventStore"
```

---

### Task 7: `Processing.Functions` — the Service Bus trigger

**Files:**
- Create: `services/Processing/Processing.Functions/Processing.Functions.csproj`
- Create: `services/Processing/Processing.Functions/Program.cs`
- Create: `services/Processing/Processing.Functions/ProcessInboundMessageFunction.cs`
- Create: `services/Processing/Processing.Functions/host.json`
- Create: `services/Processing/Processing.Functions/local.settings.json`
- Modify: `AzureSuite.slnx`

**Interfaces:**
- Consumes: `ProcessInboundMessageCommand` (Task 3), `CatalogHttpClient` (Task 4), `ServiceBusFanOutPublisher` (Task 5), `CosmosLifecycleEventStore` (Task 6).

- [ ] **Step 1: Create the Functions project**

```bash
dotnet new func -n Processing.Functions -o services/Processing/Processing.Functions --worker-runtime dotnet-isolated --target-framework net10.0
dotnet add services/Processing/Processing.Functions reference services/Processing/Processing.Application
dotnet add services/Processing/Processing.Functions reference services/Processing/Processing.Infrastructure
dotnet add services/Processing/Processing.Functions package MediatR
dotnet add services/Processing/Processing.Functions package Azure.Messaging.ServiceBus
dotnet add services/Processing/Processing.Functions package Microsoft.Azure.Cosmos
dotnet add services/Processing/Processing.Functions package Microsoft.Azure.Functions.Worker.Extensions.ServiceBus
dotnet add services/Processing/Processing.Functions reference services/Shared/AzureSuite.Observability
```

Add to `AzureSuite.slnx` under `/services/Processing/`.

- [ ] **Step 2: Wire dependency injection**

```csharp
// services/Processing/Processing.Functions/Program.cs
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using AzureSuite.Observability;
using AzureSuite.Processing.Application.Abstractions;
using AzureSuite.Processing.Application.Messages.Commands.ProcessInboundMessage;
using AzureSuite.Processing.Infrastructure.Catalog;
using AzureSuite.Processing.Infrastructure.Messaging;
using AzureSuite.Processing.Infrastructure.Persistence;
using MediatR;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((context, services) =>
    {
        services.AddAzureSuiteLogging("Processing.Functions");
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(ProcessInboundMessageCommand).Assembly));

        var configuration = context.Configuration;

        services.AddHttpClient<ICatalogClient, CatalogHttpClient>(client =>
        {
            client.BaseAddress = new Uri(configuration["CatalogApi:BaseUrl"]!);
        });

        services.AddSingleton(sp =>
        {
            var fullyQualifiedNamespace = configuration["ServiceBus:FullyQualifiedNamespace"];
            var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                ExcludeManagedIdentityCredential = context.HostingEnvironment.IsDevelopment()
            });
            return new ServiceBusClient(fullyQualifiedNamespace, credential);
        });
        services.AddSingleton<IFanOutPublisher>(sp => new ServiceBusFanOutPublisher(sp.GetRequiredService<ServiceBusClient>()));

        services.AddSingleton(sp =>
        {
            var cosmosClient = new CosmosClient(configuration["Cosmos:AccountEndpoint"], new DefaultAzureCredential());
            return cosmosClient.GetContainer(configuration["Cosmos:DatabaseName"], configuration["Cosmos:ContainerName"]);
        });
        services.AddSingleton<ILifecycleEventStore, CosmosLifecycleEventStore>();
    })
    .Build();

host.Run();
```

- [ ] **Step 3: Write the trigger function**

No unit test for this file — it's a thin adapter with no branching logic of its own (all decision-making already lives in, and is tested by, `ProcessInboundMessageCommandHandler` from Task 3). This matches the same reasoning `Ingestion.Api`'s minimal-API endpoint lambdas aren't separately unit tested either.

```csharp
// services/Processing/Processing.Functions/ProcessInboundMessageFunction.cs
using AzureSuite.Processing.Application.Messages.Commands.ProcessInboundMessage;
using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace AzureSuite.Processing.Functions
{
    public class ProcessInboundMessageFunction
    {
        private readonly IMediator _mediator;
        private readonly ILogger<ProcessInboundMessageFunction> _logger;

        public ProcessInboundMessageFunction(IMediator mediator, ILogger<ProcessInboundMessageFunction> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [Function(nameof(ProcessInboundMessageFunction))]
        public async Task Run(
            [ServiceBusTrigger("messages.raw", Connection = "ServiceBus")] ServiceBusReceivedMessage message)
        {
            var messageId = Guid.Parse(message.MessageId);
            var clientId = Guid.Parse((string)message.ApplicationProperties["clientId"]);
            var messageType = (string)message.ApplicationProperties["messageType"];
            var version = (string)message.ApplicationProperties["version"];
            var payload = message.Body.ToString();

            // Any exception here propagates and leaves the message uncompleted — Service
            // Bus's own lock/redelivery retries it. Never catch-and-swallow.
            var outcome = await _mediator.Send(new ProcessInboundMessageCommand(messageId, clientId, messageType, version, payload));

            if (outcome.WasRouted)
            {
                _logger.LogInformation("Routed message {MessageId} to {QueueNames}", messageId, string.Join(", ", outcome.QueueNames));
            }
            else
            {
                _logger.LogWarning("Rejected message {MessageId}: {Reason}", messageId, outcome.RejectionReason);
            }
        }
    }
}
```

- [ ] **Step 4: Configure `host.json` and `local.settings.json`**

```json
// services/Processing/Processing.Functions/host.json
{
  "version": "2.0",
  "logging": {
    "applicationInsights": {
      "samplingSettings": { "isEnabled": true, "excludedTypes": "Request" }
    }
  }
}
```

```json
// services/Processing/Processing.Functions/local.settings.json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "ServiceBus__fullyQualifiedNamespace": "sb-messaginghub-ingestion-dev.servicebus.windows.net",
    "CatalogApi:BaseUrl": "https://app-messaginghub-catalog-dev.azurewebsites.net",
    "Cosmos:AccountEndpoint": "https://cosmos-messaginghub-dev.documents.azure.com:443/",
    "Cosmos:DatabaseName": "messaginghub",
    "Cosmos:ContainerName": "LifecycleEvents"
  }
}
```

`local.settings.json` is gitignored by the `func` project template by default — verify with `git status` after this step; if it's untracked, that's correct, leave it out of the commit (it's local-only, same as `appsettings.Development.json` elsewhere in this repo).

- [ ] **Step 5: Build to confirm it compiles**

Run: `dotnet build services/Processing/Processing.Functions`
Expected: builds with zero warnings.

- [ ] **Step 6: Commit**

```bash
git add services/Processing/Processing.Functions/Processing.Functions.csproj services/Processing/Processing.Functions/Program.cs services/Processing/Processing.Functions/ProcessInboundMessageFunction.cs services/Processing/Processing.Functions/host.json AzureSuite.slnx
git commit -m "feat(processing): add Processing.Functions Service Bus trigger"
```

---

### Task 8: Infra — Cosmos, output queues, Function App hosting

**Files:**
- Create: `infra/modules/shared/cosmos.bicep`
- Create: `infra/modules/processing/servicebus-output.bicep`
- Create: `infra/modules/processing/storage.bicep`
- Create: `infra/modules/processing/functionapp.bicep`
- Modify: `infra/main.bicep`

**Interfaces:**
- Produces: three Service Bus queues, one Cosmos account/database/container, one Function App with role assignments granting it Service Bus receive-on-`messages.raw`/send-on-output-queues and Cosmos write access.

- [ ] **Step 1: Cosmos module**

```bicep
// infra/modules/shared/cosmos.bicep
param location string
param accountName string
param databaseName string = 'messaginghub'
param containerName string = 'LifecycleEvents'
param principalId string

resource cosmosAccount 'Microsoft.DocumentDB/databaseAccounts@2024-08-15' = {
  name: accountName
  location: location
  kind: 'GlobalDocumentDB'
  properties: {
    databaseAccountOfferType: 'Standard'
    // Serverless: cheapest option for low, bursty write volume -- matches this project's
    // free/low-cost bias elsewhere, avoids provisioning RU/s capacity nobody needs yet.
    capabilities: [
      { name: 'EnableServerless' }
    ]
    locations: [
      { locationName: location, failoverPriority: 0 }
    ]
  }
}

resource database 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2024-08-15' = {
  parent: cosmosAccount
  name: databaseName
  properties: {
    resource: { id: databaseName }
  }
}

resource container 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-08-15' = {
  parent: database
  name: containerName
  properties: {
    resource: {
      id: containerName
      partitionKey: {
        paths: ['/messageId']
        kind: 'Hash'
      }
    }
  }
}

// Cosmos data-plane access is RBAC via SQL role assignments, not the control-plane
// roleDefinitionId pattern used for Service Bus/Storage elsewhere in this repo.
resource dataContributorRoleAssignment 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2024-08-15' = {
  parent: cosmosAccount
  name: guid(cosmosAccount.id, principalId, 'CosmosDataContributor')
  properties: {
    roleDefinitionId: '${cosmosAccount.id}/sqlRoleDefinitions/00000000-0000-0000-0000-000000000002'
    principalId: principalId
    scope: cosmosAccount.id
  }
}

output accountEndpoint string = cosmosAccount.properties.documentEndpoint
output databaseName string = databaseName
output containerName string = containerName
```

- [ ] **Step 2: Output queues module**

```bicep
// infra/modules/processing/servicebus-output.bicep
param serviceBusNamespaceName string
param queueNames array = ['out-settlements', 'out-notifications', 'out-audit']
param senderPrincipalId string

resource serviceBusNamespace 'Microsoft.ServiceBus/namespaces@2024-01-01' existing = {
  name: serviceBusNamespaceName
}

resource outputQueues 'Microsoft.ServiceBus/namespaces/queues@2024-01-01' = [for queueName in queueNames: {
  parent: serviceBusNamespace
  name: queueName
  properties: {}
}]

resource dataSenderRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(serviceBusNamespace.id, senderPrincipalId, 'ServiceBusDataSenderOutput')
  scope: serviceBusNamespace
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '69a216fc-b8fb-44d8-bc22-1f3c2cd27a39')
    principalId: senderPrincipalId
    principalType: 'ServicePrincipal'
  }
}

output queueNames array = queueNames
```

- [ ] **Step 3: Storage module (required by every Function App for its runtime)**

```bicep
// infra/modules/processing/storage.bicep
param location string
param storageAccountName string

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  kind: 'StorageV2'
  sku: { name: 'Standard_LRS' }
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
  }
}

output connectionString string = 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=core.windows.net'
```

- [ ] **Step 4: Function App module**

```bicep
// infra/modules/processing/functionapp.bicep
param location string
param appServicePlanName string
param functionAppName string
param storageAccountConnectionString string
param appInsightsConnectionString string
param serviceBusFullyQualifiedNamespace string
param catalogApiBaseUrl string
param cosmosAccountEndpoint string
param cosmosDatabaseName string
param cosmosContainerName string

resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: appServicePlanName
  location: location
  sku: {
    name: 'Y1'
    tier: 'Dynamic'
  }
  kind: 'functionapp,linux'
  properties: {
    reserved: true
  }
}

resource functionApp 'Microsoft.Web/sites@2023-12-01' = {
  name: functionAppName
  location: location
  kind: 'functionapp,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    siteConfig: {
      linuxFxVersion: 'DOTNET-ISOLATED|10.0'
      appSettings: [
        { name: 'AzureWebJobsStorage', value: storageAccountConnectionString }
        { name: 'FUNCTIONS_EXTENSION_VERSION', value: '~4' }
        { name: 'FUNCTIONS_WORKER_RUNTIME', value: 'dotnet-isolated' }
        { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsightsConnectionString }
        { name: 'ServiceBus__fullyQualifiedNamespace', value: serviceBusFullyQualifiedNamespace }
        { name: 'CatalogApi:BaseUrl', value: catalogApiBaseUrl }
        { name: 'Cosmos:AccountEndpoint', value: cosmosAccountEndpoint }
        { name: 'Cosmos:DatabaseName', value: cosmosDatabaseName }
        { name: 'Cosmos:ContainerName', value: cosmosContainerName }
      ]
    }
  }
}

output principalId string = functionApp.identity.principalId
output defaultHostname string = functionApp.properties.defaultHostName
```

- [ ] **Step 5: Wire everything into `main.bicep`**

```bicep
// Add to infra/main.bicep, after the existing ingestionServiceBus module:

module processingStorage 'modules/processing/storage.bicep' = {
  name: 'processingStorage'
  params: {
    location: location
    storageAccountName: 'stmsghubprocdev'
  }
}

module processingOutputQueues 'modules/processing/servicebus-output.bicep' = {
  name: 'processingOutputQueues'
  params: {
    serviceBusNamespaceName: ingestionServiceBusNamespaceName
    senderPrincipalId: processingFunctions.outputs.principalId
  }
}

module sharedCosmos 'modules/shared/cosmos.bicep' = {
  name: 'sharedCosmos'
  params: {
    location: location
    accountName: 'cosmos-messaginghub-dev'
    principalId: processingFunctions.outputs.principalId
  }
}

module processingFunctions 'modules/processing/functionapp.bicep' = {
  name: 'processingFunctions'
  params: {
    location: location
    appServicePlanName: 'asp-messaginghub-processing-dev'
    functionAppName: 'func-messaginghub-processing-dev'
    storageAccountConnectionString: processingStorage.outputs.connectionString
    appInsightsConnectionString: sharedAppInsights.outputs.connectionString
    serviceBusFullyQualifiedNamespace: '${ingestionServiceBusNamespaceName}.servicebus.windows.net'
    catalogApiBaseUrl: 'https://${catalogApi.outputs.defaultHostname}'
    cosmosAccountEndpoint: sharedCosmos.outputs.accountEndpoint
    cosmosDatabaseName: sharedCosmos.outputs.databaseName
    cosmosContainerName: sharedCosmos.outputs.containerName
  }
}
```

Note the circular-looking reference: `processingOutputQueues` and `sharedCosmos` need `processingFunctions.outputs.principalId` for role assignments, but `processingFunctions` needs `sharedCosmos.outputs.accountEndpoint` for its app settings. Bicep resolves this fine since `processingFunctions`' own declaration doesn't depend on the role-assignment modules — but declare `processingFunctions` textually before `processingOutputQueues`/`sharedCosmos` in the file (Bicep evaluates by dependency graph, not declaration order, so this is a readability preference, not a functional requirement) and double check `az deployment group what-if` shows no cycle before applying.

Also grant `processingFunctions`' managed identity `Azure Service Bus Data Receiver` on `messages.raw` (reuse the existing `ingestionServiceBus.bicep` module's shape as a reference, adding a second role assignment resource there for the receiver role, principalId `processingFunctions.outputs.principalId`, role definition ID `4f6d3b9b-027b-4f4c-9142-0e5a2a2247e0`).

- [ ] **Step 6: Validate the Bicep compiles**

Run: `az bicep build --file infra/main.bicep`
Expected: no errors.

- [ ] **Step 7: Commit**

```bash
git add infra/modules/shared/cosmos.bicep infra/modules/processing infra/main.bicep infra/modules/ingestion/servicebus.bicep
git commit -m "feat(infra): add Cosmos, output queues, and Processing Function App"
```

---

### Task 9: CI/CD — deploy Processing.Functions

**Files:**
- Modify: `.github/workflows/build.yml`

**Interfaces:**
- Consumes: `processingFunctions` Bicep output (Task 8), same `azure-dev` GitHub environment already used by Catalog/Ingestion deploy jobs.

- [ ] **Step 1: Add the deploy job**

```yaml
# Add to .github/workflows/build.yml, mirroring deploy-ingestion-api's shape:
deploy-processing-functions:
  needs: build-and-test
  runs-on: ubuntu-latest
  environment: azure-dev
  steps:
    - uses: actions/checkout@v4
    - uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '10.0.x'
    - name: Publish
      run: dotnet publish services/Processing/Processing.Functions -c Release -o ./publish
    - name: Deploy to Azure Functions
      uses: Azure/functions-action@v1
      with:
        app-name: func-messaginghub-processing-dev
        package: ./publish
        publish-profile: ${{ secrets.AZURE_PROCESSING_FUNCTIONS_PUBLISH_PROFILE }}
```

`AZURE_PROCESSING_FUNCTIONS_PUBLISH_PROFILE` must be added as a GitHub secret on the `azure-dev` environment (download via `az functionapp deployment list-publishing-profiles --name func-messaginghub-processing-dev --xml` after Task 8's infra is deployed) — this is a manual one-time step, not something a workflow file can do for itself.

- [ ] **Step 2: Commit**

```bash
git add .github/workflows/build.yml
git commit -m "ci: deploy Processing.Functions"
```

---

## Self-Review

**Spec coverage:**
- Two single-purpose Catalog calls (validate, route-lookup), fan-out, Cosmos lifecycle recording, client-not-authorized handling → Task 3 (core logic) + Tasks 4-6 (real implementations).
- `Ingestion.Api` `ClientId` contract change → Task 1.
- Cosmos, output queues, Function App infra → Task 8.
- CI/CD → Task 9.
- Deliberately out of this plan: Delivery, Archive, Monitoring, Logic App consumer, idempotency, semantic validation — all still deferred per the spec.

**Placeholder scan:** no TBD/TODO; every step shows real code; Task 8's Bicep circular-reference note explains a real ordering subtlety rather than hand-waving it.

**Type consistency:** `ICatalogClient`/`ILifecycleEventStore`/`IFanOutPublisher` signatures from Task 2 are used identically in Tasks 3-7; `ProcessingOutcomeDto`/`LifecycleEvent`/`ValidationResult`/`RouteLookupResult` field names match across every consuming task.

---

Plan complete and saved to `docs/superpowers/plans/2026-09-23-processing-functions.md`.
