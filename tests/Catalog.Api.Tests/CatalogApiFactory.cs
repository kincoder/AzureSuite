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
