using AzureSuite.Web.Blazor.UI;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace AzureSuite.Web.Blazor.Layout
{
    /// <summary>Catches unhandled render/lifecycle exceptions from any page so a bug in one
    /// feature shows a friendly message instead of taking down the whole app, and logs the
    /// exception via telemetry the same way pages' own try/catch blocks do.</summary>
    public partial class AppErrorBoundary : ErrorBoundary
    {
        [Inject]
        private ClientTelemetryLogger ClientTelemetry { get; set; } = null!;

        protected override async Task OnErrorAsync(Exception exception)
        {
            await ClientTelemetry.LogExceptionAsync(exception);
            await base.OnErrorAsync(exception);
        }
    }
}
