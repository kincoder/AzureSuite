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
    /// Test host for <see cref="Program"/> that always uses a fresh, empty EF Core InMemory
    /// database — regardless of any real "CatalogDb" connection string configured via user
    /// secrets, and regardless of Program.cs's own InMemory seed data. Keeps integration tests
    /// hermetic and independent of Azure SQL and of what's configured on the machine running
    /// them.
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
            // immediately during CreateBuilder, so setting them here (before the factory boots
            // the host on first use) is visible in time.
            //
            // Whether appsettings.Development.json's WriteTo:0 Event Log entry is actually in
            // effect depends on both the file being present (it's gitignored, so it's absent in
            // a CI checkout) AND ASPNETCORE_ENVIRONMENT being Development (a Production-style run
            // even on a machine with the file physically present would load appsettings.json's
            // empty WriteTo instead) — too many combinations to branch on correctly here. Instead
            // these three env vars unconditionally define the entire WriteTo:0 entry from
            // scratch: always exactly one Event Log sink, source "Catalog.Api", with source
            // management disabled so its constructor never calls the permission-restricted
            // EventLog.SourceExists check. This is deterministic regardless of environment, OS,
            // or which appsettings files exist, and matches (or harmlessly duplicates) whatever
            // appsettings.Development.json declares when it does apply.
            Environment.SetEnvironmentVariable("Serilog__WriteTo__0__Name", "EventLog");
            Environment.SetEnvironmentVariable("Serilog__WriteTo__0__Args__source", "Catalog.Api");
            Environment.SetEnvironmentVariable("Serilog__WriteTo__0__Args__manageEventSource", "false");

            // Program.cs reads builder.Configuration.GetConnectionString("CatalogDb") right
            // after CreateBuilder, in the same "too early for WebApplicationFactory's deferred
            // config overrides" window as the Serilog setup above — so ConfigureServices below
            // (which fully replaces the DbContext registration either way) doesn't stop
            // Program.cs's own usingInMemory branch from also running its InMemorySeedData.Apply
            // against whatever database it thinks it's using. On a machine with no real
            // "CatalogDb" connection string configured anywhere (i.e. any clean checkout, CI
            // included), usingInMemory is true there too, so that seeding runs and inserts
            // pacs.008/1.0 and camt.054/1.0 before the tests get a chance to register their own
            // — conflicting with RegisterThenGet_ReturnsTheRegisteredMessageType. Forcing a
            // non-empty (unused) connection string via environment variable makes Program.cs
            // skip that branch entirely, regardless of what's configured locally.
            Environment.SetEnvironmentVariable("ConnectionStrings__CatalogDb", "Server=unused;Database=unused;");
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
