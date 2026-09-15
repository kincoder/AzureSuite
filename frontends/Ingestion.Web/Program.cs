using AzureSuite.Ingestion.Web.Services;
using AzureSuite.Web.UI;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;

namespace AzureSuite.Ingestion.Web
{
    /// <summary>Entry point that bootstraps the Ingestion.Web Blazor WebAssembly host.</summary>
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.RootComponents.RegisterCustomElement<App>("ingestion-app");

            var ingestionApiBaseUrl = builder.Configuration["IngestionApiBaseUrl"];
            builder.Services.AddScoped(sp => new HttpClient
            {
                BaseAddress = new Uri(string.IsNullOrWhiteSpace(ingestionApiBaseUrl) ? "https://localhost:7185" : ingestionApiBaseUrl)
            });
            builder.Services.AddScoped<IngestionApiClient>();
            builder.Services.AddScoped<ClientTelemetryLogger>();

            var host = builder.Build();

            var jsRuntime = host.Services.GetRequiredService<IJSRuntime>();
            await jsRuntime.InvokeVoidAsync("initAppInsights", builder.Configuration["ApplicationInsightsConnectionString"], "Ingestion.Web");

            await host.RunAsync();
        }
    }
}
