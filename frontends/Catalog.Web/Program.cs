using AzureSuite.Catalog.Web.Services;
using AzureSuite.Web.UI;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;

namespace AzureSuite.Catalog.Web
{
    /// <summary>Entry point that bootstraps the Catalog.Web Blazor WebAssembly host.</summary>
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.RootComponents.RegisterCustomElement<App>("catalog-app");

            builder.Services.AddScoped(sp => new HttpClient
            {
                BaseAddress = new Uri(builder.Configuration["CatalogApiBaseUrl"] ?? "https://localhost:7184")
            });
            builder.Services.AddScoped<CatalogApiClient>();
            builder.Services.AddScoped<ClientTelemetryLogger>();

            var host = builder.Build();

            var jsRuntime = host.Services.GetRequiredService<IJSRuntime>();
            await jsRuntime.InvokeVoidAsync("initAppInsights", builder.Configuration["ApplicationInsightsConnectionString"], "Catalog.Web");

            await host.RunAsync();
        }
    }
}
