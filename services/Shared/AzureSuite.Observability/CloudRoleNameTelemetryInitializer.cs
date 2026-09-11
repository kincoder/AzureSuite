using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;

namespace AzureSuite.Observability;

/// <summary>
/// Tags every telemetry item with a fixed "cloud role name" so multiple services sharing
/// one Application Insights resource stay distinguishable in the Application Map, Live
/// Metrics, and log queries (cloud_RoleName == roleName).
/// </summary>
public sealed class CloudRoleNameTelemetryInitializer : ITelemetryInitializer
{
    private readonly string _roleName;

    public CloudRoleNameTelemetryInitializer(string roleName)
    {
        _roleName = roleName;
    }

    public void Initialize(ITelemetry telemetry)
    {
        telemetry.Context.Cloud.RoleName = _roleName;
    }
}
