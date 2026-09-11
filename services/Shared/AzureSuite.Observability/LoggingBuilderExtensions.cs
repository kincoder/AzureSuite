using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Builder;
using Serilog;
using Serilog.Sinks.ApplicationInsights.TelemetryConverters;

namespace AzureSuite.Observability;

/// <summary>Wires Serilog for a service, with sink selection driven entirely by that
/// service's own appsettings configuration (see the "Serilog" config section) rather
/// than environment checks in code.</summary>
public static class LoggingBuilderExtensions
{
    public static WebApplicationBuilder AddAzureSuiteLogging(this WebApplicationBuilder builder, string serviceName)
    {
        var loggerConfiguration = new LoggerConfiguration()
            .ReadFrom.Configuration(builder.Configuration)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Service", serviceName);

        var connectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            var telemetryConfiguration = TelemetryConfiguration.CreateDefault();
            telemetryConfiguration.ConnectionString = connectionString;
            telemetryConfiguration.TelemetryInitializers.Add(new CloudRoleNameTelemetryInitializer(serviceName));

            loggerConfiguration = loggerConfiguration.WriteTo.ApplicationInsights(
                telemetryConfiguration,
                TelemetryConverter.Traces);
        }

        Log.Logger = loggerConfiguration.CreateLogger();
        builder.Host.UseSerilog();

        return builder;
    }
}
