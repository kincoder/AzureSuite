using Microsoft.JSInterop;

namespace AzureSuite.Web.UI;

/// <summary>Forwards a caught .NET exception to Application Insights (via the shared
/// applicationInsights.js) so it shows up under Failures/Exceptions in Azure, not just the
/// browser console. No-ops if Application Insights was never initialized for this MFE.</summary>
public sealed class ClientTelemetryLogger
{
    private readonly IJSRuntime _jsRuntime;

    public ClientTelemetryLogger(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task LogExceptionAsync(Exception exception)
    {
        await _jsRuntime.InvokeVoidAsync("logException", exception.Message, exception.GetType().Name);
    }
}
