using AzureSuite.Web.Blazor.Features.Catalog.Services;
using AzureSuite.Web.Blazor.Features.Ingestion.Services;
using AzureSuite.Web.Blazor.UI;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;

namespace AzureSuite.Web.Blazor
{
    /// <summary>Entry point that bootstraps the AzureSuite.Web Blazor WebAssembly host, a
    /// single app covering both the Catalog and Ingestion features.</summary>
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.RootComponents.Add<App>("#app");

            // Typed HttpClient registrations: each API client gets its own configured
            // HttpClient instance. A plain `AddScoped(_ => new HttpClient {...})` per client
            // would collide here, since both API clients now live in the same DI container
            // and are both constructor-injected with the unqualified `HttpClient` type --
            // the second registration would silently win for both.
            var catalogApiBaseUrl = builder.Configuration["CatalogApiBaseUrl"];
            builder.Services.AddHttpClient<CatalogApiClient>(client =>
            {
                client.BaseAddress = new Uri(string.IsNullOrWhiteSpace(catalogApiBaseUrl) ? "https://localhost:7184" : catalogApiBaseUrl);
            });

            var ingestionApiBaseUrl = builder.Configuration["IngestionApiBaseUrl"];
            builder.Services.AddHttpClient<IngestionApiClient>(client =>
            {
                client.BaseAddress = new Uri(string.IsNullOrWhiteSpace(ingestionApiBaseUrl) ? "https://localhost:7185" : ingestionApiBaseUrl);
            });

            builder.Services.AddScoped<ClientTelemetryLogger>();

            var host = builder.Build();

            var jsRuntime = host.Services.GetRequiredService<IJSRuntime>();
            await jsRuntime.InvokeVoidAsync("initAppInsights", builder.Configuration["ApplicationInsightsConnectionString"], "AzureSuite.Web");

            await host.RunAsync();
        }
    }
}
