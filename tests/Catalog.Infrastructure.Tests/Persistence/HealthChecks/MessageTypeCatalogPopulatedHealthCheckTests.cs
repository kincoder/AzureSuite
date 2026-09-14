using AzureSuite.Catalog.Domain.Entities;
using AzureSuite.Catalog.Domain.ValueObjects;
using AzureSuite.Catalog.Infrastructure.Persistence;
using AzureSuite.Catalog.Infrastructure.Persistence.HealthChecks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace Catalog.Infrastructure.Tests.Persistence.HealthChecks
{
    public class MessageTypeCatalogPopulatedHealthCheckTests
    {
        private static CatalogDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<CatalogDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            return new CatalogDbContext(options);
        }

        [Fact]
        public async Task CheckHealthAsync_WhenTableHasRows_ReturnsHealthy()
        {
            await using var context = CreateContext();
            context.MessageTypes.Add(new MessageType(new MessageTypeName("pacs.008"), new MessageTypeVersion("1.0"), "{}"));
            await context.SaveChangesAsync();
            var healthCheck = new MessageTypeCatalogPopulatedHealthCheck(context);

            var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

            result.Status.Should().Be(HealthStatus.Healthy);
        }

        [Fact]
        public async Task CheckHealthAsync_WhenTableIsEmpty_ReturnsDegraded()
        {
            await using var context = CreateContext();
            var healthCheck = new MessageTypeCatalogPopulatedHealthCheck(context);

            var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

            result.Status.Should().Be(HealthStatus.Degraded);
        }

        [Fact]
        public async Task CheckHealthAsync_WhenQueryThrows_ReturnsUnhealthy()
        {
            await using var context = CreateContext();
            await context.DisposeAsync();
            var healthCheck = new MessageTypeCatalogPopulatedHealthCheck(context);

            var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

            result.Status.Should().Be(HealthStatus.Unhealthy);
            result.Exception.Should().NotBeNull();
        }
    }
}
