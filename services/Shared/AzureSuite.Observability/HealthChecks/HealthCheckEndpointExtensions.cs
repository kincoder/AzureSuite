using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Primitives;

namespace AzureSuite.Observability.HealthChecks;

/// <summary>Wires a consistent pair of health endpoints for every service: a minimal one
/// for uptime probes and a verbose one for diagnosing which specific check failed. Both
/// accept an optional <c>?tags=</c> query string (comma-separated) to run only the checks
/// carrying at least one of the requested tags — e.g. <c>/health?tags=data</c> to check
/// just the catalog-populated check without also touching SQL connectivity.
///
/// Bypasses <c>MapHealthChecks</c> deliberately: its <c>Predicate</c> is fixed at startup,
/// so it can't filter per-request from a query string. Calling <see cref="HealthCheckService"/>
/// directly gives us that.</summary>
public static class HealthCheckEndpointExtensions
{
    public static IEndpointRouteBuilder MapAzureSuiteHealthChecks(this IEndpointRouteBuilder endpoints, string basePath = "/health")
    {
        endpoints.MapGet(basePath, (HttpContext context, HealthCheckService healthCheckService) =>
            HandleAsync(context, healthCheckService, verbose: false));

        endpoints.MapGet($"{basePath}/verbose", (HttpContext context, HealthCheckService healthCheckService) =>
            HandleAsync(context, healthCheckService, verbose: true));

        return endpoints;
    }

    private static async Task HandleAsync(HttpContext context, HealthCheckService healthCheckService, bool verbose)
    {
        var predicate = BuildTagPredicate(context.Request.Query["tags"]);
        var report = await healthCheckService.CheckHealthAsync(predicate, context.RequestAborted);

        context.Response.StatusCode = report.Status == HealthStatus.Unhealthy
            ? StatusCodes.Status503ServiceUnavailable
            : StatusCodes.Status200OK;
        context.Response.ContentType = "application/json";

        var payload = verbose ? BuildVerbosePayload(report) : BuildMinimalPayload(report);
        await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }

    private static Func<HealthCheckRegistration, bool>? BuildTagPredicate(StringValues tagsQuery)
    {
        if (StringValues.IsNullOrEmpty(tagsQuery))
        {
            return null;
        }

        var requestedTags = tagsQuery
            .SelectMany(value => value!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return requestedTags.Count == 0
            ? null
            : registration => registration.Tags.Overlaps(requestedTags);
    }

    private static object BuildMinimalPayload(HealthReport report)
    {
        return new
        {
            status = report.Status.ToString()
        };
    }

    private static object BuildVerbosePayload(HealthReport report)
    {
        return new
        {
            status = report.Status.ToString(),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description,
                durationMs = entry.Value.Duration.TotalMilliseconds,
                tags = entry.Value.Tags,
                exception = entry.Value.Exception?.Message
            })
        };
    }
}
