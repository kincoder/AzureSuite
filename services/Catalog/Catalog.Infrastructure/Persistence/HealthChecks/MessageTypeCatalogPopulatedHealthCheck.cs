using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AzureSuite.Catalog.Infrastructure.Persistence.HealthChecks
{
    /// <summary>Distinguishes "SQL is reachable" from "the catalog actually has data" — an
    /// empty MessageTypes table is a real (if degraded) state, not the same failure as the
    /// table/schema being missing entirely.</summary>
    public class MessageTypeCatalogPopulatedHealthCheck : IHealthCheck
    {
        private readonly CatalogDbContext _context;

        public MessageTypeCatalogPopulatedHealthCheck(CatalogDbContext context)
        {
            _context = context;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                var hasAnyMessageTypes = await _context.MessageTypes.AnyAsync(cancellationToken);
                return hasAnyMessageTypes
                    ? HealthCheckResult.Healthy("Catalog contains at least one registered message type.")
                    : HealthCheckResult.Degraded("Catalog table is reachable but contains no message types.");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("Failed to query the MessageTypes table.", ex);
            }
        }
    }
}
