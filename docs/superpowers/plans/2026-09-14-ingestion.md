# Ingestion Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the Ingestion service's front door: `Ingestion.Api` accepts a message envelope over HTTP, mints a `MessageId`, and durably publishes it to an Azure Service Bus queue — plus `Ingestion.Web`, a minimal test-submission form, and the infra/CI-CD to deploy both.

**Architecture:** Mirrors Catalog's existing shape exactly — MediatR command/handler in an Application project, an Infrastructure project implementing an Application-defined abstraction, a minimal-API `Api` project, a Blazor WASM `Web` project reusing `AzureSuite.Web.UI`. The one new piece is `IMessagePublisher` → `ServiceBusMessagePublisher`, publishing to a Basic-tier Service Bus queue via managed identity (no connection string).

**Tech Stack:** .NET 10, MediatR, `Azure.Messaging.ServiceBus` + `Azure.Identity` (`DefaultAzureCredential`), xUnit + FluentAssertions + bunit, Bicep, GitHub Actions.

**Spec:** `docs/superpowers/specs/2026-09-14-ingestion-design.md`

## Global Constraints

- Warning-free, nullable-enabled, no `#pragma warning disable` (EF-generated files don't apply here — Ingestion has no EF Core).
- Block-scoped namespaces (`namespace X { ... }`), not file-scoped — matches every `services/Catalog/*` and `frontends/Catalog.Web/*` file.
- No top-level statements — every `Program.cs` has explicit `class Program` + `static void Main`.
- Minimal API endpoints, not `[ApiController]` classes.
- XML doc comments on every class and any non-obvious property.
- Request DTOs live in their own `Contracts/` folder, not inline in `Program.cs`.
- 1:1 `tests/X.Y.Tests` project per `src`/`services` project, mirroring folder structure, one test class per production class — except `Program.cs` (composition root, covered by integration tests, not unit tests) and `ServiceBusMessagePublisher` (thin delegation to the Azure SDK with no branching logic of its own; the SDK's send behavior isn't meaningfully unit-testable without a live/emulated broker, and this repo has neither wired up — covered instead by the CI smoke test hitting the real deployed queue).
- Hand-written fakes before reaching for a mocking library.
- `dotnet build AzureSuite.slnx --configuration Release` must show `0 Warning(s)` and `0 Error(s)` after every task.

---

### Task 1: `Ingestion.Application` — command, handler, publisher abstraction

**Files:**
- Create: `services/Ingestion/Ingestion.Application/Ingestion.Application.csproj`
- Create: `services/Ingestion/Ingestion.Application/Abstractions/IMessagePublisher.cs`
- Create: `services/Ingestion/Ingestion.Application/Messages/IngestedMessageDto.cs`
- Create: `services/Ingestion/Ingestion.Application/Messages/Commands/IngestMessage/IngestMessageCommand.cs`
- Create: `services/Ingestion/Ingestion.Application/Messages/Commands/IngestMessage/IngestMessageCommandHandler.cs`
- Test: `tests/Ingestion.Application.Tests/Ingestion.Application.Tests.csproj`
- Test: `tests/Ingestion.Application.Tests/Messages/Commands/IngestMessage/IngestMessageCommandHandlerTests.cs`
- Modify: `AzureSuite.slnx`

**Interfaces:**
- Produces: `IMessagePublisher.PublishAsync(Guid messageId, string messageType, string version, string payload, CancellationToken cancellationToken) -> Task`
- Produces: `IngestMessageCommand(string MessageType, string Version, string Payload) : IRequest<IngestedMessageDto>`
- Produces: `IngestedMessageDto(Guid MessageId, string MessageType, string Version)`
- Produces: `IngestMessageCommandHandler : IRequestHandler<IngestMessageCommand, IngestedMessageDto>` — constructor takes `(IMessagePublisher publisher, ILogger<IngestMessageCommandHandler> logger)`. Logs publish success (with latency) and publish failure (with exception) — see the design spec's Observability section.

- [ ] **Step 1: Create the project file**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <PackageReference Include="MediatR" Version="14.2.0" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.0" />
  </ItemGroup>

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

</Project>
```

Save as `services/Ingestion/Ingestion.Application/Ingestion.Application.csproj`. (The handler logs publish success/failure per the design spec's Observability section — `Microsoft.Extensions.Logging.Abstractions` is the package that gives `ILogger<T>` without pulling in a concrete logging framework, matching this Application layer's existing "no infrastructure dependencies" discipline.)

- [ ] **Step 2: Write `IMessagePublisher`**

```csharp
namespace AzureSuite.Ingestion.Application.Abstractions
{
    /// <summary>
    /// Publishing contract for accepted messages, defined here (Application layer) and
    /// implemented in Infrastructure — command handlers never depend on the Service Bus SDK
    /// directly, matching Catalog's IMessageTypeRepository split.
    /// </summary>
    public interface IMessagePublisher
    {
        /// <summary>Publishes an accepted message. Does not return until the broker has
        /// durably accepted it — callers rely on this to know whether it's safe to
        /// acknowledge the original request.</summary>
        Task PublishAsync(Guid messageId, string messageType, string version, string payload, CancellationToken cancellationToken);
    }
}
```

Save as `services/Ingestion/Ingestion.Application/Abstractions/IMessagePublisher.cs`.

- [ ] **Step 3: Write `IngestedMessageDto`**

```csharp
namespace AzureSuite.Ingestion.Application.Messages
{
    /// <summary>
    /// Returned to the caller once a message has been accepted and durably published.
    /// </summary>
    /// <param name="MessageId">The identifier minted for this message — a UUIDv7, sortable
    /// by acceptance time.</param>
    /// <param name="MessageType">The message type as submitted, e.g. "pacs.008".</param>
    /// <param name="Version">The schema version as submitted, e.g. "1.0".</param>
    public record IngestedMessageDto(Guid MessageId, string MessageType, string Version);
}
```

Save as `services/Ingestion/Ingestion.Application/Messages/IngestedMessageDto.cs`.

- [ ] **Step 4: Write `IngestMessageCommand`**

```csharp
using AzureSuite.Ingestion.Application.Messages;
using MediatR;

namespace AzureSuite.Ingestion.Application.Messages.Commands.IngestMessage
{
    /// <summary>Accepts a raw message for publishing. No validation beyond "these fields are
    /// present" happens here or in its handler — see the Ingestion design spec for why identification,
    /// structural, and semantic validation are all deliberately deferred to a future downstream stage.</summary>
    /// <param name="MessageType">Declared message type, e.g. "pacs.008".</param>
    /// <param name="Version">Declared schema version, e.g. "1.0".</param>
    /// <param name="Payload">The raw message payload as submitted.</param>
    public record IngestMessageCommand(string MessageType, string Version, string Payload) : IRequest<IngestedMessageDto>;
}
```

Save as `services/Ingestion/Ingestion.Application/Messages/Commands/IngestMessage/IngestMessageCommand.cs`.

- [ ] **Step 5: Write the failing test for the handler**

```csharp
using AzureSuite.Ingestion.Application.Abstractions;
using AzureSuite.Ingestion.Application.Messages.Commands.IngestMessage;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ingestion.Application.Tests.Messages.Commands.IngestMessage
{
    public class IngestMessageCommandHandlerTests
    {
        private sealed class FakeMessagePublisher : IMessagePublisher
        {
            public Guid? PublishedMessageId { get; private set; }
            public string? PublishedMessageType { get; private set; }
            public string? PublishedVersion { get; private set; }
            public string? PublishedPayload { get; private set; }

            public Task PublishAsync(Guid messageId, string messageType, string version, string payload, CancellationToken cancellationToken)
            {
                PublishedMessageId = messageId;
                PublishedMessageType = messageType;
                PublishedVersion = version;
                PublishedPayload = payload;
                return Task.CompletedTask;
            }
        }

        [Fact]
        public async Task Handle_PublishesTheMessageAndReturnsItsMintedId()
        {
            var publisher = new FakeMessagePublisher();
            var handler = new AzureSuite.Ingestion.Application.Messages.Commands.IngestMessage.IngestMessageCommandHandler(publisher, NullLogger<AzureSuite.Ingestion.Application.Messages.Commands.IngestMessage.IngestMessageCommandHandler>.Instance);
            var command = new AzureSuite.Ingestion.Application.Messages.Commands.IngestMessage.IngestMessageCommand("pacs.008", "1.0", "{}");

            var result = await handler.Handle(command, CancellationToken.None);

            result.MessageType.Should().Be("pacs.008");
            result.Version.Should().Be("1.0");
            result.MessageId.Should().NotBeEmpty();
            publisher.PublishedMessageId.Should().Be(result.MessageId);
            publisher.PublishedMessageType.Should().Be("pacs.008");
            publisher.PublishedVersion.Should().Be("1.0");
            publisher.PublishedPayload.Should().Be("{}");
        }

        [Fact]
        public async Task Handle_MintsADifferentIdForEachCall()
        {
            var publisher = new FakeMessagePublisher();
            var handler = new AzureSuite.Ingestion.Application.Messages.Commands.IngestMessage.IngestMessageCommandHandler(publisher, NullLogger<AzureSuite.Ingestion.Application.Messages.Commands.IngestMessage.IngestMessageCommandHandler>.Instance);
            var command = new AzureSuite.Ingestion.Application.Messages.Commands.IngestMessage.IngestMessageCommand("pacs.008", "1.0", "{}");

            var first = await handler.Handle(command, CancellationToken.None);
            var second = await handler.Handle(command, CancellationToken.None);

            first.MessageId.Should().NotBe(second.MessageId);
        }
    }
}
```

Save as `tests/Ingestion.Application.Tests/Messages/Commands/IngestMessage/IngestMessageCommandHandlerTests.cs`.

- [ ] **Step 6: Create the test project file**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="10.0.1">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="FluentAssertions" Version="8.10.0" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.10.0" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="4.0.0">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\services\Ingestion\Ingestion.Application\Ingestion.Application.csproj" />
  </ItemGroup>

</Project>
```

Save as `tests/Ingestion.Application.Tests/Ingestion.Application.Tests.csproj`.

- [ ] **Step 7: Add both new projects to `AzureSuite.slnx`**

Open `AzureSuite.slnx`. Add a new `/services/Ingestion/` folder alongside the existing `/services/Catalog/` one, and list the test project under `/tests/`:

```xml
  <Folder Name="/services/Ingestion/">
    <Project Path="services/Ingestion/Ingestion.Application/Ingestion.Application.csproj" />
  </Folder>
```

Add this folder block right after the `/services/Catalog/` folder block closes. Add the test project line inside the existing `/tests/` folder block, alongside the `Catalog.Application.Tests` line:

```xml
    <Project Path="tests/Ingestion.Application.Tests/Ingestion.Application.Tests.csproj" />
```

- [ ] **Step 8: Run the test to verify it fails (project doesn't build yet — handler missing)**

Run: `dotnet test tests/Ingestion.Application.Tests/Ingestion.Application.Tests.csproj`
Expected: build error — `IngestMessageCommandHandler` does not exist.

- [ ] **Step 9: Write the handler**

```csharp
using System.Diagnostics;
using AzureSuite.Ingestion.Application.Abstractions;
using AzureSuite.Ingestion.Application.Messages;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AzureSuite.Ingestion.Application.Messages.Commands.IngestMessage
{
    public class IngestMessageCommandHandler : IRequestHandler<IngestMessageCommand, IngestedMessageDto>
    {
        private readonly IMessagePublisher _publisher;
        private readonly ILogger<IngestMessageCommandHandler> _logger;

        public IngestMessageCommandHandler(IMessagePublisher publisher, ILogger<IngestMessageCommandHandler> logger)
        {
            _publisher = publisher;
            _logger = logger;
        }

        public async Task<IngestedMessageDto> Handle(IngestMessageCommand request, CancellationToken cancellationToken)
        {
            var messageId = Guid.CreateVersion7();
            var stopwatch = Stopwatch.StartNew();

            try
            {
                await _publisher.PublishAsync(messageId, request.MessageType, request.Version, request.Payload, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to publish message {MessageId} of type {MessageType} v{Version}",
                    messageId, request.MessageType, request.Version);
                throw;
            }

            _logger.LogInformation(
                "Published message {MessageId} of type {MessageType} v{Version} in {ElapsedMilliseconds}ms",
                messageId, request.MessageType, request.Version, stopwatch.ElapsedMilliseconds);

            return new IngestedMessageDto(messageId, request.MessageType, request.Version);
        }
    }
}
```

Save as `services/Ingestion/Ingestion.Application/Messages/Commands/IngestMessage/IngestMessageCommandHandler.cs`. (Structured log properties — `{MessageId}`, `{MessageType}`, etc. — not string interpolation, so they stay filterable fields in App Insights rather than being flattened into the message text, per the design spec's Observability section.)

- [ ] **Step 10: Run the test to verify it passes**

Run: `dotnet test tests/Ingestion.Application.Tests/Ingestion.Application.Tests.csproj`
Expected: `Passed! - Failed: 0, Passed: 2`

- [ ] **Step 11: Commit**

```bash
git add services/Ingestion/Ingestion.Application tests/Ingestion.Application.Tests AzureSuite.slnx
git commit -m "Add Ingestion.Application: IngestMessageCommand + handler + publisher abstraction"
```

---

### Task 2: `Ingestion.Infrastructure` — Service Bus publisher

**Files:**
- Create: `services/Ingestion/Ingestion.Infrastructure/Ingestion.Infrastructure.csproj`
- Create: `services/Ingestion/Ingestion.Infrastructure/Messaging/ServiceBusMessagePublisher.cs`
- Modify: `AzureSuite.slnx`

**Interfaces:**
- Consumes: `IMessagePublisher` (Task 1).
- Produces: `ServiceBusMessagePublisher : IMessagePublisher, IAsyncDisposable` — constructor `(ServiceBusClient client, string queueName)`.

No dedicated test file for this task — see Global Constraints for why (thin delegation to the Azure SDK, no unit-testable logic of its own).

- [ ] **Step 1: Create the project and add the Azure SDK packages**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <ProjectReference Include="..\Ingestion.Application\Ingestion.Application.csproj" />
  </ItemGroup>

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

</Project>
```

Save as `services/Ingestion/Ingestion.Infrastructure/Ingestion.Infrastructure.csproj`. Then run:

```bash
dotnet add services/Ingestion/Ingestion.Infrastructure/Ingestion.Infrastructure.csproj package Azure.Messaging.ServiceBus
```

- [ ] **Step 2: Write `ServiceBusMessagePublisher`**

```csharp
using AzureSuite.Ingestion.Application.Abstractions;
using Azure.Messaging.ServiceBus;

namespace AzureSuite.Ingestion.Infrastructure.Messaging
{
    /// <summary>Publishes accepted messages to the raw Service Bus queue. Stamps the minted
    /// MessageId onto the broker message's own native MessageId property — this is also the
    /// property Service Bus's built-in duplicate detection keys on, should that be enabled
    /// later (Standard tier only; not enabled this increment, see the Ingestion design spec).</summary>
    public class ServiceBusMessagePublisher : IMessagePublisher, IAsyncDisposable
    {
        private readonly ServiceBusSender _sender;

        public ServiceBusMessagePublisher(ServiceBusClient client, string queueName)
        {
            _sender = client.CreateSender(queueName);
        }

        public async Task PublishAsync(Guid messageId, string messageType, string version, string payload, CancellationToken cancellationToken)
        {
            var message = new ServiceBusMessage(payload)
            {
                MessageId = messageId.ToString()
            };
            message.ApplicationProperties["messageType"] = messageType;
            message.ApplicationProperties["version"] = version;

            await _sender.SendMessageAsync(message, cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            await _sender.DisposeAsync();
        }
    }
}
```

Save as `services/Ingestion/Ingestion.Infrastructure/Messaging/ServiceBusMessagePublisher.cs`.

- [ ] **Step 3: Add the project to `AzureSuite.slnx`**

Inside the `/services/Ingestion/` folder block added in Task 1:

```xml
    <Project Path="services/Ingestion/Ingestion.Infrastructure/Ingestion.Infrastructure.csproj" />
```

- [ ] **Step 4: Build to verify it compiles**

Run: `dotnet build services/Ingestion/Ingestion.Infrastructure/Ingestion.Infrastructure.csproj`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 5: Commit**

```bash
git add services/Ingestion/Ingestion.Infrastructure AzureSuite.slnx
git commit -m "Add Ingestion.Infrastructure: ServiceBusMessagePublisher"
```

---

### Task 3: `Ingestion.Api` — endpoint, DI wiring, health check

**Files:**
- Create: `services/Ingestion/Ingestion.Api/Ingestion.Api.csproj`
- Create: `services/Ingestion/Ingestion.Api/Contracts/IngestMessageRequest.cs`
- Create: `services/Ingestion/Ingestion.Api/Program.cs`
- Create: `services/Ingestion/Ingestion.Api/appsettings.json`
- Create: `services/Ingestion/Ingestion.Api/Properties/launchSettings.json`
- Test: `tests/Ingestion.Api.Tests/Ingestion.Api.Tests.csproj`
- Test: `tests/Ingestion.Api.Tests/IngestionApiFactory.cs`
- Test: `tests/Ingestion.Api.Tests/MessagesEndpointTests.cs`
- Modify: `AzureSuite.slnx`

**Interfaces:**
- Consumes: `IngestMessageCommand`/`IngestedMessageDto` (Task 1), `IMessagePublisher`/`ServiceBusMessagePublisher` (Tasks 1–2), `MapAzureSuiteHealthChecks()` (already in `AzureSuite.Observability.HealthChecks`, used by Catalog.Api).
- Produces: `POST /messages` → `201 { messageId, messageType, version }` on success, `400` on missing fields. `GET /health`, `GET /health/verbose` (no custom checks registered — bare health-check infrastructure, consistent with Catalog and required by this service's CI smoke test in Task 5).

- [ ] **Step 1: Create the project file**

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <UserSecretsId>a3f8c9e2-6b1d-4e7a-9c3f-2d8b5e1a7f4c</UserSecretsId>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MediatR" Version="14.2.0" />
    <PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="10.0.12" />
    <PackageReference Include="Scalar.AspNetCore" Version="2.17.3" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\Ingestion.Application\Ingestion.Application.csproj" />
    <ProjectReference Include="..\Ingestion.Infrastructure\Ingestion.Infrastructure.csproj" />
    <ProjectReference Include="..\..\Shared\AzureSuite.Observability\AzureSuite.Observability.csproj" />
  </ItemGroup>

</Project>
```

Save as `services/Ingestion/Ingestion.Api/Ingestion.Api.csproj`. Then run:

```bash
dotnet add services/Ingestion/Ingestion.Api/Ingestion.Api.csproj package Azure.Identity
```

(`Azure.Messaging.ServiceBus` comes in transitively via the `Ingestion.Infrastructure` project reference, but `Program.cs` also constructs a `ServiceBusClient` directly for DI registration, so it needs the package too:)

```bash
dotnet add services/Ingestion/Ingestion.Api/Ingestion.Api.csproj package Azure.Messaging.ServiceBus
```

- [ ] **Step 2: Write the request contract**

```csharp
namespace AzureSuite.Ingestion.Api.Contracts
{
    /// <summary>Request body for <c>POST /messages</c>. All three fields are required — this
    /// is the only validation Ingestion performs; see the Ingestion design spec for why
    /// deeper validation is deliberately not done here.</summary>
    public record IngestMessageRequest(string MessageType, string Version, string Payload);
}
```

Save as `services/Ingestion/Ingestion.Api/Contracts/IngestMessageRequest.cs`.

- [ ] **Step 3: Write `appsettings.json`**

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ApplicationInsights": {
    "ConnectionString": ""
  },
  "Serilog": {
    "MinimumLevel": "Information",
    "WriteTo": []
  },
  "ServiceBus": {
    "FullyQualifiedNamespace": "",
    "QueueName": "messages.raw"
  }
}
```

Save as `services/Ingestion/Ingestion.Api/appsettings.json`.

- [ ] **Step 4: Write `launchSettings.json`**

```json
{
  "$schema": "https://json.schemastore.org/launchsettings.json",
  "profiles": {
    "http": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": false,
      "applicationUrl": "http://localhost:5027",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    },
    "https": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": false,
      "applicationUrl": "https://localhost:7185;http://localhost:5027",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
```

Save as `services/Ingestion/Ingestion.Api/Properties/launchSettings.json`. (Ports 5027/7185 — one above Catalog.Api's 5026/7184 — so both can run side by side locally.)

- [ ] **Step 5: Write the failing integration test**

```csharp
using System.Net;
using System.Net.Http.Json;
using AzureSuite.Ingestion.Application.Messages;
using FluentAssertions;
using Xunit;

namespace Ingestion.Api.Tests
{
    public class MessagesEndpointTests : IClassFixture<IngestionApiFactory>
    {
        private readonly HttpClient _client;

        public MessagesEndpointTests(IngestionApiFactory factory)
        {
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task Post_WithAllFieldsPresent_ReturnsCreatedWithMessageId()
        {
            var response = await _client.PostAsJsonAsync("/messages", new
            {
                messageType = "pacs.008",
                version = "1.0",
                payload = "{}"
            });

            response.StatusCode.Should().Be(HttpStatusCode.Created);
            var dto = await response.Content.ReadFromJsonAsync<IngestedMessageDto>();
            dto!.MessageId.Should().NotBeEmpty();
            dto.MessageType.Should().Be("pacs.008");
            dto.Version.Should().Be("1.0");
        }

        [Theory]
        [InlineData("", "1.0", "{}")]
        [InlineData("pacs.008", "", "{}")]
        [InlineData("pacs.008", "1.0", "")]
        public async Task Post_WithAMissingField_ReturnsBadRequest(string messageType, string version, string payload)
        {
            var response = await _client.PostAsJsonAsync("/messages", new { messageType, version, payload });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Health_ReturnsOk()
        {
            var response = await _client.GetAsync("/health");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }
}
```

Save as `tests/Ingestion.Api.Tests/MessagesEndpointTests.cs`.

- [ ] **Step 6: Write the test factory, substituting a fake publisher**

```csharp
using AzureSuite.Ingestion.Api;
using AzureSuite.Ingestion.Application.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ingestion.Api.Tests
{
    /// <summary>Test host for <see cref="Program"/> that substitutes a no-op fake publisher
    /// in place of ServiceBusMessagePublisher, so integration tests never touch a real Service
    /// Bus namespace — mirrors CatalogApiFactory's InMemory-database substitution.</summary>
    public class IngestionApiFactory : WebApplicationFactory<Program>
    {
        private sealed class FakeMessagePublisher : IMessagePublisher
        {
            public Task PublishAsync(Guid messageId, string messageType, string version, string payload, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }
        }

        public IngestionApiFactory()
        {
            Environment.SetEnvironmentVariable("Serilog__WriteTo__0__Name", "EventLog");
            Environment.SetEnvironmentVariable("Serilog__WriteTo__0__Args__source", "Ingestion.Api");
            Environment.SetEnvironmentVariable("Serilog__WriteTo__0__Args__manageEventSource", "false");
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IMessagePublisher>();
                services.AddSingleton<IMessagePublisher, FakeMessagePublisher>();
            });
        }
    }
}
```

Save as `tests/Ingestion.Api.Tests/IngestionApiFactory.cs`.

- [ ] **Step 7: Create the test project file**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="10.0.1">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="FluentAssertions" Version="8.10.0" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.12" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.10.0" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="4.0.0">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\services\Ingestion\Ingestion.Api\Ingestion.Api.csproj" />
  </ItemGroup>

</Project>
```

Save as `tests/Ingestion.Api.Tests/Ingestion.Api.Tests.csproj`.

- [ ] **Step 8: Add both to `AzureSuite.slnx`**

In the `/services/Ingestion/` folder block:

```xml
    <Project Path="services/Ingestion/Ingestion.Api/Ingestion.Api.csproj" />
```

In the `/tests/` folder block:

```xml
    <Project Path="tests/Ingestion.Api.Tests/Ingestion.Api.Tests.csproj" />
```

- [ ] **Step 9: Run the tests to verify they fail (Program.cs doesn't exist yet)**

Run: `dotnet test tests/Ingestion.Api.Tests/Ingestion.Api.Tests.csproj`
Expected: build error — no `Program` type in `Ingestion.Api`.

- [ ] **Step 10: Write `Program.cs`**

```csharp
using AzureSuite.Ingestion.Api.Contracts;
using AzureSuite.Ingestion.Application.Abstractions;
using AzureSuite.Ingestion.Application.Messages.Commands.IngestMessage;
using AzureSuite.Ingestion.Infrastructure.Messaging;
using AzureSuite.Observability;
using AzureSuite.Observability.HealthChecks;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using MediatR;
using Scalar.AspNetCore;

namespace AzureSuite.Ingestion.Api
{
    /// <summary>Entry point and endpoint registration for the Ingestion service's HTTP API.</summary>
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.AddAzureSuiteLogging("Ingestion.Api");

            builder.Services.AddOpenApi();
            builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(IngestMessageCommand).Assembly));

            builder.Services.AddSingleton(sp =>
            {
                var fullyQualifiedNamespace = builder.Configuration["ServiceBus:FullyQualifiedNamespace"];
                return new ServiceBusClient(fullyQualifiedNamespace, new DefaultAzureCredential());
            });
            builder.Services.AddSingleton<IMessagePublisher>(sp =>
            {
                var client = sp.GetRequiredService<ServiceBusClient>();
                var queueName = builder.Configuration["ServiceBus:QueueName"]!;
                return new ServiceBusMessagePublisher(client, queueName);
            });

            builder.Services.AddHealthChecks();

            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    policy.WithOrigins(
                            "https://localhost:7205",
                            "http://localhost:5147")
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                });
            });

            var app = builder.Build();

            app.UseCors();

            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
                app.MapScalarApiReference();
            }

            app.UseHttpsRedirection();

            app.MapAzureSuiteHealthChecks();

            app.MapPost("/messages", async (IngestMessageRequest request, IMediator mediator, ILogger<Program> logger) =>
            {
                logger.LogInformation("Received submission for type {MessageType} v{Version}", request.MessageType, request.Version);

                if (string.IsNullOrWhiteSpace(request.MessageType) ||
                    string.IsNullOrWhiteSpace(request.Version) ||
                    string.IsNullOrWhiteSpace(request.Payload))
                {
                    logger.LogWarning(
                        "Rejected submission for type {MessageType} v{Version}: a required field was missing",
                        request.MessageType, request.Version);
                    return Results.BadRequest("messageType, version, and payload are all required.");
                }

                var dto = await mediator.Send(new IngestMessageCommand(request.MessageType, request.Version, request.Payload));
                return Results.Created($"/messages/{dto.MessageId}", dto);
            });

            app.Run();
        }
    }
}
```

Save as `services/Ingestion/Ingestion.Api/Program.cs`.

- [ ] **Step 11: Run the tests to verify they pass**

Run: `dotnet test tests/Ingestion.Api.Tests/Ingestion.Api.Tests.csproj`
Expected: `Passed! - Failed: 0, Passed: 5`

- [ ] **Step 12: Build the whole solution to catch any cross-project break**

Run: `dotnet build AzureSuite.slnx --configuration Release`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 13: Commit**

```bash
git add services/Ingestion/Ingestion.Api tests/Ingestion.Api.Tests AzureSuite.slnx
git commit -m "Add Ingestion.Api: POST /messages endpoint, health checks, Service Bus DI wiring"
```

---

### Task 4: Infra — Service Bus, App Service, Static Web App for Ingestion

**Files:**
- Create: `infra/modules/ingestion/servicebus.bicep`
- Create: `infra/modules/ingestion/appservice.bicep`
- Create: `infra/modules/ingestion/staticwebapp.bicep`
- Modify: `infra/main.bicep`

**Interfaces:**
- Produces (from `appservice.bicep`): `output defaultHostname string`, `output principalId string`.
- Produces (from `servicebus.bicep`): `output fullyQualifiedNamespace string`, `output queueName string`.
- Produces (from `staticwebapp.bicep`): `output defaultHostname string`.

- [ ] **Step 1: Write the App Service module**

```bicep
param location string
param appServicePlanName string
param webAppName string
param appInsightsConnectionString string
param serviceBusFullyQualifiedNamespace string
param serviceBusQueueName string

resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: appServicePlanName
  location: location
  sku: {
    name: 'F1'
    tier: 'Free'
  }
  kind: 'linux'
  properties: {
    reserved: true
  }
}

resource webApp 'Microsoft.Web/sites@2023-12-01' = {
  name: webAppName
  location: location
  kind: 'app,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      appCommandLine: 'dotnet Ingestion.Api.dll'
      appSettings: [
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsightsConnectionString
        }
        {
          name: 'ServiceBus__FullyQualifiedNamespace'
          value: serviceBusFullyQualifiedNamespace
        }
        {
          name: 'ServiceBus__QueueName'
          value: serviceBusQueueName
        }
        {
          name: 'SCM_DO_BUILD_DURING_DEPLOYMENT'
          value: 'false'
        }
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Development'
        }
      ]
    }
  }
}

output defaultHostname string = webApp.properties.defaultHostName
output principalId string = webApp.identity.principalId
```

Save as `infra/modules/ingestion/appservice.bicep`. (Mirrors `infra/modules/catalog/appservice.bicep` exactly — same F1/startup-command/Oryx fixes already proven there, same `ASPNETCORE_ENVIRONMENT=Development` choice to keep `/scalar/v1` reachable, per your "just want to make sure it's running and visible" call on Catalog.)

- [ ] **Step 2: Write the Service Bus module**

```bicep
param location string
param serviceBusNamespaceName string
param queueName string = 'messages.raw'
param apiPrincipalId string

resource serviceBusNamespace 'Microsoft.ServiceBus/namespaces@2024-01-01' = {
  name: serviceBusNamespaceName
  location: location
  sku: {
    name: 'Basic'
    tier: 'Basic'
  }
}

resource queue 'Microsoft.ServiceBus/namespaces/queues@2024-01-01' = {
  parent: serviceBusNamespace
  name: queueName
  properties: {}
}

resource dataSenderRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(serviceBusNamespace.id, apiPrincipalId, 'ServiceBusDataSender')
  scope: serviceBusNamespace
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '69a216fc-b8fb-44d8-bc22-1f3c2cd27a39')
    principalId: apiPrincipalId
    principalType: 'ServicePrincipal'
  }
}

output fullyQualifiedNamespace string = '${serviceBusNamespace.name}.servicebus.windows.net'
output queueName string = queue.name
```

Save as `infra/modules/ingestion/servicebus.bicep`. (Basic tier — no topics, no built-in duplicate detection; both Standard-only, deliberately deferred per the Ingestion design spec. Role `69a216fc-b8fb-44d8-bc22-1f3c2cd27a39` is Azure's built-in "Azure Service Bus Data Sender.")

- [ ] **Step 3: Write the Static Web App module**

```bicep
param location string
param staticWebAppName string

resource staticWebApp 'Microsoft.Web/staticSites@2023-12-01' = {
  name: staticWebAppName
  location: location
  sku: {
    name: 'Free'
    tier: 'Free'
  }
  properties: {}
}

output defaultHostname string = staticWebApp.properties.defaultHostname
```

Save as `infra/modules/ingestion/staticwebapp.bicep`. (Identical to `infra/modules/catalog/staticwebapp.bicep`.)

- [ ] **Step 4: Wire the new modules into `main.bicep`**

Open `infra/main.bicep`. There's a circular-dependency-avoidance pattern already in use for Catalog's Key Vault (a `var` holding the name, passed to both the module that creates the resource and the module that needs to reference it by name, rather than an ARM cross-reference) — apply the same idea here: the App Service module needs the Service Bus namespace's expected FQDN as a literal string *before* the Service Bus module runs, and the Service Bus module needs the App Service's `principalId` *after* the App Service module runs. So: define the namespace name as a `var`, create the App Service module first, then the Service Bus module (passing the App Service's `principalId` output).

Add near the top, alongside the existing `catalogKeyVaultName` var:

```bicep
var ingestionServiceBusNamespaceName = 'sb-messaginghub-ingestion-dev'
```

Add these modules (placement doesn't matter relative to the Catalog modules — after them is fine):

```bicep
module ingestionApi 'modules/ingestion/appservice.bicep' = {
  name: 'ingestionApi'
  params: {
    location: location
    appServicePlanName: 'asp-messaginghub-ingestion-dev'
    webAppName: 'app-messaginghub-ingestion-dev'
    appInsightsConnectionString: sharedAppInsights.outputs.connectionString
    serviceBusFullyQualifiedNamespace: '${ingestionServiceBusNamespaceName}.servicebus.windows.net'
    serviceBusQueueName: 'messages.raw'
  }
}

module ingestionServiceBus 'modules/ingestion/servicebus.bicep' = {
  name: 'ingestionServiceBus'
  params: {
    location: location
    serviceBusNamespaceName: ingestionServiceBusNamespaceName
    apiPrincipalId: ingestionApi.outputs.principalId
  }
}

module ingestionStaticWebApp 'modules/ingestion/staticwebapp.bicep' = {
  name: 'ingestionStaticWebApp'
  params: {
    location: location
    staticWebAppName: 'stapp-messaginghub-ingestion-dev'
  }
}
```

Add outputs alongside the existing Catalog ones:

```bicep
output ingestionApiHostname string = ingestionApi.outputs.defaultHostname
output ingestionStaticWebAppHostname string = ingestionStaticWebApp.outputs.defaultHostname
```

- [ ] **Step 5: Validate the template compiles**

Run: `az bicep build --file infra/main.bicep --stdout`
Expected: no errors (a `use-parent-property` or similar linter warning, if any, is fine — only hard errors block this step).

- [ ] **Step 6: Commit**

```bash
git add infra/modules/ingestion infra/main.bicep
git commit -m "Add Ingestion infra: Service Bus (Basic, queue), App Service, Static Web App"
```

---

### Task 5: CI/CD — deploy jobs and smoke tests for Ingestion

**Files:**
- Modify: `.github/workflows/build.yml`

**Interfaces:**
- Consumes: `azure-dev` GitHub Environment (already exists), the same smoke-test pattern already built for `deploy-catalog-api`/`deploy-catalog-web`.
- Produces: new `azure-dev` variables `INGESTION_API_BASE_URL`, `INGESTION_WEB_BASE_URL` (values set manually after first infra deploy — same bootstrapping sequence `CATALOG_API_BASE_URL` went through).

- [ ] **Step 1: Add `deploy-ingestion-api`, mirroring `deploy-catalog-api`**

Open `.github/workflows/build.yml`. After the existing `deploy-catalog-api` job, add:

```yaml
  deploy-ingestion-api:
    needs: build-and-test
    if: github.ref == 'refs/heads/master' && github.event_name == 'push'
    runs-on: ubuntu-latest
    name: Deploy Ingestion.Api
    environment: azure-dev

    permissions:
      contents: read

    steps:
      - name: Checkout
        uses: actions/checkout@v7

      - name: Setup .NET
        uses: actions/setup-dotnet@v6
        with:
          dotnet-version: '10.0.x'

      - name: Publish
        run: dotnet publish services/Ingestion/Ingestion.Api/Ingestion.Api.csproj --configuration Release --output ./publish

      - name: Deploy to Azure App Service
        uses: azure/webapps-deploy@v3
        with:
          app-name: app-messaginghub-ingestion-dev
          publish-profile: ${{ secrets.AZURE_INGESTION_API_PUBLISH_PROFILE }}
          package: ./publish

      - name: Verify deployment health
        run: |
          for i in $(seq 1 10); do
            status=$(curl -sS -o /dev/null -m 15 -w "%{http_code}" "${{ vars.INGESTION_API_BASE_URL }}/health" || echo "000")
            if [ "$status" = "200" ]; then
              echo "Health check passed"
              exit 0
            fi
            echo "Attempt $i: got HTTP $status, retrying in 15s..."
            sleep 15
          done

          echo "Health check failed after retries"
          curl -sS -m 15 "${{ vars.INGESTION_API_BASE_URL }}/health/verbose" || true
          exit 1
```

- [ ] **Step 2: Add `deploy-ingestion-web`, mirroring `deploy-catalog-web`**

Add after `deploy-ingestion-api`:

```yaml
  deploy-ingestion-web:
    needs: build-and-test
    if: github.ref == 'refs/heads/master' && github.event_name == 'push'
    runs-on: ubuntu-latest
    name: Deploy Ingestion.Web
    environment: azure-dev

    permissions:
      contents: read

    steps:
      - name: Checkout
        uses: actions/checkout@v7

      - name: Substitute environment configuration
        run: |
          sed -i \
            -e "s|__INGESTION_API_BASE_URL__|${{ vars.INGESTION_API_BASE_URL }}|" \
            -e "s|__APPLICATION_INSIGHTS_CONNECTION_STRING__|${{ vars.APPLICATION_INSIGHTS_CONNECTION_STRING }}|" \
            frontends/Ingestion.Web/wwwroot/appsettings.json

      - name: Deploy to Azure Static Web Apps
        uses: Azure/static-web-apps-deploy@v1
        with:
          azure_static_web_apps_api_token: ${{ secrets.AZURE_INGESTION_STATIC_WEB_APPS_API_TOKEN }}
          repo_token: ${{ secrets.GITHUB_TOKEN }}
          action: upload
          app_location: frontends/Ingestion.Web
          output_location: wwwroot

      - name: Verify deployment
        run: |
          for i in $(seq 1 10); do
            content=$(curl -sS -m 15 "${{ vars.INGESTION_WEB_BASE_URL }}/appsettings.json" || echo "")
            if [ -n "$content" ]; then
              break
            fi
            echo "Attempt $i: site not responding yet, retrying in 15s..."
            sleep 15
          done

          if [ -z "$content" ]; then
            echo "Site never responded"
            exit 1
          fi

          if echo "$content" | grep -q "__INGESTION_API_BASE_URL__"; then
            echo "appsettings.json still contains an unsubstituted placeholder"
            exit 1
          fi

          if ! echo "$content" | grep -qF "${{ vars.INGESTION_API_BASE_URL }}"; then
            echo "appsettings.json does not contain the expected API base URL"
            exit 1
          fi

          echo "Deployment verified: appsettings.json correctly substituted"
```

- [ ] **Step 3: Validate the workflow YAML has no structural errors**

There's no local YAML linter in this repo (confirmed during the earlier infra CI/CD work — `js-yaml`/`PyYAML` aren't installed). Visually diff the new jobs against `deploy-catalog-api`/`deploy-catalog-web` for matching indentation (2 spaces per level, consistent with the rest of the file) before committing.

- [ ] **Step 4: Commit**

```bash
git add .github/workflows/build.yml
git commit -m "Add deploy-ingestion-api and deploy-ingestion-web CI jobs with smoke tests"
```

**Note for whoever merges this branch:** before these jobs can succeed, three things need to happen manually (same bootstrapping Catalog went through) — apply the Task 4 bicep to Azure, add `AZURE_INGESTION_API_PUBLISH_PROFILE` and `AZURE_INGESTION_STATIC_WEB_APPS_API_TOKEN` as `azure-dev`-scoped secrets, and set `INGESTION_API_BASE_URL`/`INGESTION_WEB_BASE_URL` as `azure-dev`-scoped variables once the resources' actual hostnames are known.

---

### Task 6: `Ingestion.Web` — test-submission form

**Files:**
- Create: `frontends/Ingestion.Web/Ingestion.Web.csproj`
- Create: `frontends/Ingestion.Web/Program.cs`
- Create: `frontends/Ingestion.Web/App.razor`
- Create: `frontends/Ingestion.Web/_Imports.razor`
- Create: `frontends/Ingestion.Web/Contracts/IngestMessageRequest.cs`
- Create: `frontends/Ingestion.Web/Services/IngestionApiClient.cs`
- Create: `frontends/Ingestion.Web/Pages/SubmitMessage.razor`
- Create: `frontends/Ingestion.Web/Pages/SubmitMessage.razor.cs`
- Create: `frontends/Ingestion.Web/wwwroot/index.html`
- Create: `frontends/Ingestion.Web/wwwroot/appsettings.json`
- Create: `frontends/Ingestion.Web/wwwroot/staticwebapp.config.json`
- Create: `frontends/Ingestion.Web/wwwroot/favicon.ico` (copy from `frontends/Catalog.Web/wwwroot/favicon.ico`)
- Create: `frontends/Ingestion.Web/Properties/launchSettings.json`
- Test: `tests/Ingestion.Web.Tests/Ingestion.Web.Tests.csproj`
- Test: `tests/Ingestion.Web.Tests/Services/IngestionApiClientTests.cs`
- Test: `tests/Ingestion.Web.Tests/Pages/SubmitMessageRenderTests.cs`
- Modify: `AzureSuite.slnx`

**Interfaces:**
- Consumes: `AzureSuite.Web.UI`'s `PageHeader`, `AppButton`, `ErrorState` components and `ClientTelemetryLogger` (all already used by `Catalog.Web`); `AzureSuite.Ingestion.Application.Messages.IngestedMessageDto` (Task 1, for deserializing the API response).
- Produces: `IngestMessageRequest { MessageType, Version, Payload }` (plain class, settable properties — matches Catalog.Web's `RegisterMessageTypeRequest` shape, needed for `EditForm`/`@bind-Value`). `IngestionApiClient.SubmitMessageAsync(IngestMessageRequest, CancellationToken) -> Task<IngestedMessageDto>`.

- [ ] **Step 1: Create the project file**

```xml
<Project Sdk="Microsoft.NET.Sdk.BlazorWebAssembly">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <RootNamespace>AzureSuite.Ingestion.Web</RootNamespace>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Components.CustomElements" Version="10.0.12" />
    <PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly" Version="10.0.12" />
    <PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly.DevServer" Version="10.0.12" PrivateAssets="all" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\services\Ingestion\Ingestion.Application\Ingestion.Application.csproj" />
    <ProjectReference Include="..\AzureSuite.Web.UI\AzureSuite.Web.UI.csproj" />
  </ItemGroup>

</Project>
```

Save as `frontends/Ingestion.Web/Ingestion.Web.csproj`.

- [ ] **Step 2: Write the request contract**

```csharp
namespace AzureSuite.Ingestion.Web.Contracts
{
    /// <summary>Request body sent to Ingestion.Api's POST /messages endpoint.</summary>
    public class IngestMessageRequest
    {
        public string MessageType { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Payload { get; set; } = string.Empty;
    }
}
```

Save as `frontends/Ingestion.Web/Contracts/IngestMessageRequest.cs`.

- [ ] **Step 3: Write the failing test for the API client**

```csharp
using System.Net;
using System.Net.Http.Json;
using AzureSuite.Ingestion.Application.Messages;
using AzureSuite.Ingestion.Web.Contracts;
using AzureSuite.Ingestion.Web.Services;
using FluentAssertions;
using Xunit;

namespace Ingestion.Web.Tests.Services
{
    public class IngestionApiClientTests
    {
        private sealed class FakeHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

            public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
            {
                _respond = respond;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(_respond(request));
            }
        }

        [Fact]
        public async Task SubmitMessageAsync_ReturnsDeserializedDto()
        {
            var expected = new IngestedMessageDto(Guid.NewGuid(), "pacs.008", "1.0");
            var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = JsonContent.Create(expected)
            });
            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost") };
            var client = new IngestionApiClient(httpClient);

            var result = await client.SubmitMessageAsync(
                new IngestMessageRequest { MessageType = "pacs.008", Version = "1.0", Payload = "{}" },
                CancellationToken.None);

            result.Should().BeEquivalentTo(expected);
        }

        [Fact]
        public async Task SubmitMessageAsync_ThrowsHttpRequestException_WhenApiReturnsBadRequest()
        {
            var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest));
            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost") };
            var client = new IngestionApiClient(httpClient);

            var act = () => client.SubmitMessageAsync(
                new IngestMessageRequest { MessageType = "pacs.008", Version = "1.0", Payload = "{}" },
                CancellationToken.None);

            await act.Should().ThrowAsync<HttpRequestException>();
        }
    }
}
```

Save as `tests/Ingestion.Web.Tests/Services/IngestionApiClientTests.cs`.

- [ ] **Step 4: Create the test project file**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="bunit" Version="2.10.3" />
    <PackageReference Include="coverlet.collector" Version="10.0.1">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="FluentAssertions" Version="8.10.0" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.10.0" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="4.0.0">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\frontends\Ingestion.Web\Ingestion.Web.csproj" />
  </ItemGroup>

</Project>
```

Save as `tests/Ingestion.Web.Tests/Ingestion.Web.Tests.csproj`.

- [ ] **Step 5: Add both projects to `AzureSuite.slnx`**

In the `/frontends/` folder block, alongside `Catalog.Web`:

```xml
    <Project Path="frontends/Ingestion.Web/Ingestion.Web.csproj" />
```

In the `/tests/` folder block:

```xml
    <Project Path="tests/Ingestion.Web.Tests/Ingestion.Web.Tests.csproj" />
```

- [ ] **Step 6: Run the test to verify it fails**

Run: `dotnet test tests/Ingestion.Web.Tests/Ingestion.Web.Tests.csproj`
Expected: build error — `IngestionApiClient` does not exist.

- [ ] **Step 7: Write `IngestionApiClient`**

```csharp
using System.Net.Http.Json;
using AzureSuite.Ingestion.Application.Messages;
using AzureSuite.Ingestion.Web.Contracts;

namespace AzureSuite.Ingestion.Web.Services
{
    /// <summary>Thin wrapper over Ingestion.Api's HTTP endpoint.</summary>
    public class IngestionApiClient
    {
        private readonly HttpClient _httpClient;

        public IngestionApiClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <summary>Submits a message for ingestion.</summary>
        public async Task<IngestedMessageDto> SubmitMessageAsync(IngestMessageRequest request, CancellationToken cancellationToken)
        {
            var response = await _httpClient.PostAsJsonAsync("/messages", request, cancellationToken);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<IngestedMessageDto>(cancellationToken))!;
        }
    }
}
```

Save as `frontends/Ingestion.Web/Services/IngestionApiClient.cs`.

- [ ] **Step 8: Run the test to verify it passes**

Run: `dotnet test tests/Ingestion.Web.Tests/Ingestion.Web.Tests.csproj`
Expected: `Passed! - Failed: 0, Passed: 2`

- [ ] **Step 9: Write `_Imports.razor`**

```razor
@using System.Net.Http
@using System.Net.Http.Json
@using Microsoft.AspNetCore.Components.Forms
@using Microsoft.AspNetCore.Components.Routing
@using Microsoft.AspNetCore.Components.Web
@using Microsoft.AspNetCore.Components.Web.Virtualization
@using Microsoft.AspNetCore.Components.WebAssembly.Http
@using Microsoft.JSInterop
@using AzureSuite.Ingestion.Web
@using AzureSuite.Web.UI.Components
```

Save as `frontends/Ingestion.Web/_Imports.razor`.

- [ ] **Step 10: Write `App.razor`**

```razor
@* Root component of Ingestion.Web, registered as the <ingestion-app> custom element in Program.cs. *@
<Router AppAssembly="@typeof(App).Assembly">
    <Found Context="routeData">
        <RouteView RouteData="@routeData" />
        <FocusOnNavigate RouteData="@routeData" Selector="h1" />
    </Found>
    <NotFound>
        <p>Page not found.</p>
    </NotFound>
</Router>
```

Save as `frontends/Ingestion.Web/App.razor`.

- [ ] **Step 11: Write `Pages/SubmitMessage.razor`**

```razor
@page "/"

<PageHeader Title="Submit Message" Eyebrow="Ingestion" />

<EditForm Model="@Request" OnValidSubmit="@SubmitAsync">
    <div class="form-group">
        <label>Message Type</label>
        <InputText @bind-Value="Request.MessageType" />
    </div>
    <div class="form-group">
        <label>Version</label>
        <InputText @bind-Value="Request.Version" />
    </div>
    <div class="form-group">
        <label>Payload</label>
        <InputTextArea @bind-Value="Request.Payload" />
    </div>
    <AppButton Type="submit">Submit</AppButton>
</EditForm>

@if (ErrorMessage is not null)
{
    <ErrorState Message="@ErrorMessage" />
}

@if (SubmittedMessage is not null)
{
    <p class="subtitle">@SubmittedMessage</p>
}
```

Save as `frontends/Ingestion.Web/Pages/SubmitMessage.razor`.

- [ ] **Step 12: Write `Pages/SubmitMessage.razor.cs`**

```csharp
using AzureSuite.Ingestion.Web.Contracts;
using AzureSuite.Ingestion.Web.Services;
using AzureSuite.Web.UI;
using Microsoft.AspNetCore.Components;

namespace AzureSuite.Ingestion.Web.Pages
{
    /// <summary>Presents a form to submit a test message to Ingestion.Api. Serves as the
    /// interim producer-simulation tool per the master design spec, ahead of a real
    /// diagnostics dashboard once downstream lifecycle data exists to back one.</summary>
    public partial class SubmitMessage : ComponentBase
    {
        [Inject]
        private IngestionApiClient ApiClient { get; set; } = null!;

        [Inject]
        private ClientTelemetryLogger ClientTelemetry { get; set; } = null!;

        private IngestMessageRequest Request { get; set; } = new();

        private string? SubmittedMessage { get; set; }

        private string? ErrorMessage { get; set; }

        private async Task SubmitAsync()
        {
            try
            {
                var dto = await ApiClient.SubmitMessageAsync(Request, CancellationToken.None);
                SubmittedMessage = $"Accepted as {dto.MessageId}.";
                ErrorMessage = null;
                Request = new IngestMessageRequest();
            }
            catch (HttpRequestException ex)
            {
                ErrorMessage = "Failed to submit the message. Check the required fields, or the server may be unavailable.";
                await ClientTelemetry.LogExceptionAsync(ex);
            }
        }
    }
}
```

Save as `frontends/Ingestion.Web/Pages/SubmitMessage.razor.cs`.

- [ ] **Step 13: Write the render tests**

```csharp
using AzureSuite.Ingestion.Web.Pages;
using AzureSuite.Ingestion.Web.Services;
using AzureSuite.Web.UI;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AzureSuite.Ingestion.Web.Tests.Pages;

public class SubmitMessageRenderTests : BunitContext
{
    public SubmitMessageRenderTests()
    {
        Services.AddScoped(_ => new IngestionApiClient(new HttpClient { BaseAddress = new Uri("https://localhost/") }));
        Services.AddScoped(_ => new ClientTelemetryLogger(JSInterop.JSRuntime));
    }

    [Fact]
    public void RendersThreeFormGroupsAndAPrimarySubmitButton()
    {
        var cut = Render<SubmitMessage>();

        cut.FindAll(".form-group").Should().HaveCount(3);
        var submit = cut.Find("button[type=submit]");
        submit.ClassList.Should().Contain("app-button-primary");
        submit.TextContent.Should().Be("Submit");
    }
}
```

Save as `tests/Ingestion.Web.Tests/Pages/SubmitMessageRenderTests.cs`.

- [ ] **Step 14: Run the render tests to verify they pass**

Run: `dotnet test tests/Ingestion.Web.Tests/Ingestion.Web.Tests.csproj`
Expected: `Passed! - Failed: 0, Passed: 3`

- [ ] **Step 15: Write `Program.cs`**

```csharp
using AzureSuite.Ingestion.Web.Services;
using AzureSuite.Web.UI;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;

namespace AzureSuite.Ingestion.Web
{
    /// <summary>Entry point that bootstraps the Ingestion.Web Blazor WebAssembly host.</summary>
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.RootComponents.RegisterCustomElement<App>("ingestion-app");

            var ingestionApiBaseUrl = builder.Configuration["IngestionApiBaseUrl"];
            builder.Services.AddScoped(sp => new HttpClient
            {
                BaseAddress = new Uri(string.IsNullOrWhiteSpace(ingestionApiBaseUrl) ? "https://localhost:7185" : ingestionApiBaseUrl)
            });
            builder.Services.AddScoped<IngestionApiClient>();
            builder.Services.AddScoped<ClientTelemetryLogger>();

            var host = builder.Build();

            var jsRuntime = host.Services.GetRequiredService<IJSRuntime>();
            await jsRuntime.InvokeVoidAsync("initAppInsights", builder.Configuration["ApplicationInsightsConnectionString"], "Ingestion.Web");

            await host.RunAsync();
        }
    }
}
```

Save as `frontends/Ingestion.Web/Program.cs`.

- [ ] **Step 16: Write `wwwroot/index.html`**

```html
<!DOCTYPE html>
<html>
<head>
    <meta charset="utf-8" />
    <title>Ingestion</title>
    <base href="/" />
    <link rel="icon" type="image/x-icon" href="favicon.ico" />
    <link rel="preconnect" href="https://fonts.googleapis.com">
    <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
    <link href="https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&display=swap" rel="stylesheet">
    <link href="_content/AzureSuite.Web.UI/theme.css" rel="stylesheet" />
    <script src="_content/AzureSuite.Web.UI/applicationInsights.js"></script>
</head>
<body>
    <ingestion-app>Loading...</ingestion-app>

    <div id="blazor-error-ui">
        An unhandled error has occurred.
        <a href="" class="reload">Reload</a>
        <a class="dismiss">🗙</a>
    </div>

    <script src="_framework/blazor.webassembly.js"></script>
</body>
</html>
```

Save as `frontends/Ingestion.Web/wwwroot/index.html`.

- [ ] **Step 17: Write `wwwroot/appsettings.json`**

```json
{
  "IngestionApiBaseUrl": "__INGESTION_API_BASE_URL__",
  "ApplicationInsightsConnectionString": "__APPLICATION_INSIGHTS_CONNECTION_STRING__"
}
```

Save as `frontends/Ingestion.Web/wwwroot/appsettings.json`.

- [ ] **Step 18: Write `wwwroot/staticwebapp.config.json`**

```json
{
  "navigationFallback": {
    "rewrite": "/index.html",
    "exclude": ["/_framework/*", "*.{css,js,wasm,json,png,ico}"]
  }
}
```

Save as `frontends/Ingestion.Web/wwwroot/staticwebapp.config.json`.

- [ ] **Step 19: Copy the favicon**

```bash
cp frontends/Catalog.Web/wwwroot/favicon.ico frontends/Ingestion.Web/wwwroot/favicon.ico
```

- [ ] **Step 20: Write `Properties/launchSettings.json`**

```json
{
  "$schema": "https://json.schemastore.org/launchsettings.json",
  "profiles": {
    "http": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": true,
      "inspectUri": "{wsProtocol}://{url.hostname}:{url.port}/_framework/debug/ws-proxy?browser={browserInspectUri}",
      "applicationUrl": "http://localhost:5147",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    },
    "https": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": true,
      "inspectUri": "{wsProtocol}://{url.hostname}:{url.port}/_framework/debug/ws-proxy?browser={browserInspectUri}",
      "applicationUrl": "https://localhost:7205;http://localhost:5147",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
```

Save as `frontends/Ingestion.Web/Properties/launchSettings.json`. (Ports 5147/7205 — one above Catalog.Web's 5146/7204 — matching the CORS origins already set in Task 3's `Program.cs`.)

- [ ] **Step 21: Build the whole solution**

Run: `dotnet build AzureSuite.slnx --configuration Release`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 22: Run the whole solution's tests**

Run: `dotnet test AzureSuite.slnx --configuration Release --no-build`
Expected: every test project passes, `Failed: 0` throughout.

- [ ] **Step 23: Commit**

```bash
git add frontends/Ingestion.Web tests/Ingestion.Web.Tests AzureSuite.slnx
git commit -m "Add Ingestion.Web: message submission form"
```

---

## After all tasks

At this point `feature/ingestion` has a complete, independently-buildable-and-testable slice: `Ingestion.Api`, `Ingestion.Web`, their infra, and their CI/CD, all mirroring Catalog's established patterns. Per your instruction, this branch is not merged into `develop` as part of this plan — integration happens later, at your call.
