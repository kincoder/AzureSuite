using AzureSuite.Observability;
using FluentAssertions;
using Microsoft.ApplicationInsights.DataContracts;

namespace AzureSuite.Observability.Tests;

public class CloudRoleNameTelemetryInitializerTests
{
    [Fact]
    public void InitializeSetsCloudRoleNameOnTelemetryContext()
    {
        var initializer = new CloudRoleNameTelemetryInitializer("Catalog.Api");
        var telemetry = new TraceTelemetry("test message");

        initializer.Initialize(telemetry);

        telemetry.Context.Cloud.RoleName.Should().Be("Catalog.Api");
    }
}
