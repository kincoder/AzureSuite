using AzureSuite.Catalog.Api;
using AzureSuite.Catalog.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Catalog.Api.Tests
{
    /// <summary>
    /// Test host for <see cref="Program"/> that always uses a fresh EF Core InMemory database,
    /// regardless of any real connection string configured via user secrets/environment. Keeps
    /// integration tests hermetic and independent of Azure SQL.
    /// </summary>
    public class CatalogApiFactory : WebApplicationFactory<Program>
    {
        public CatalogApiFactory()
        {
            // AzureSuite.Observability's AddAzureSuiteLogging runs synchronously right after
            // WebApplicationBuilder.CreateBuilder, before WebApplicationFactory's deferred
            // ConfigureWebHost/ConfigureAppConfiguration overrides are applied (those only
            // take effect when the host is actually built) — so overriding config that way
            // arrives too late to affect Serilog's setup. Environment variables, by contrast,
            // are part of WebApplicationBuilder's default configuration sources added
            // immediately during CreateBuilder, so setting one here (before the factory boots
            // the host on first use) is visible in time. This disables the Event Log sink's
            // source management, which calls EventLog.SourceExists at startup and needs
            // permissions to enumerate Windows event sources that a test run (local or CI)
            // may not have; an unmanaged, unregistered source then just fails silently per
            // write, which Serilog already tolerates.
            //
            // appsettings.Development.json is gitignored (it carries a real local Application
            // Insights connection string) and won't exist in a CI checkout, so its
            // Serilog:WriteTo:0 Event Log entry — the one this override targets — won't exist
            // there either. Only set the override when that entry actually exists: setting
            // Serilog:WriteTo:0:Args:manageEventSource when there's no WriteTo:0 to merge into
            // creates a config entry with Args but no Name, which crashes Serilog's config
            // reader ("has no 'Name' element") instead of just doing nothing.
            var developmentSettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.Development.json");
            if (File.Exists(developmentSettingsPath))
            {
                Environment.SetEnvironmentVariable("Serilog__WriteTo__0__Args__manageEventSource", "false");
            }
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                // AddDbContext registers DbContextOptions<T> plus an additive
                // IDbContextOptionsConfiguration<T> entry per provider configured; both must be
                // removed, otherwise the original (SqlServer) and this override (InMemory) both
                // apply to the same options and EF Core refuses to pick a provider.
                services.RemoveAll<DbContextOptions<CatalogDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<CatalogDbContext>>();

                // Computed once per factory instance, not per request: the lambda below runs on
                // every DbContext construction (i.e. every request), so a Guid generated inside
                // it would give each request its own empty database.
                var databaseName = Guid.NewGuid().ToString();
                services.AddDbContext<CatalogDbContext>(options => options.UseInMemoryDatabase(databaseName));
            });
        }
    }
}
