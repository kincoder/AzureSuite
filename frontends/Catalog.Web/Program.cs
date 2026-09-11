using AzureSuite.Catalog.Web.Services;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

namespace AzureSuite.Catalog.Web
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.RootComponents.Add<App>("#app");

            builder.Services.AddScoped(sp => new HttpClient
            {
                BaseAddress = new Uri(builder.Configuration["CatalogApiBaseUrl"] ?? "https://localhost:7184")
            });
            builder.Services.AddScoped<CatalogApiClient>();

            await builder.Build().RunAsync();
        }
    }
}
