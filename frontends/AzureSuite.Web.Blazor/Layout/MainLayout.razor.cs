using Microsoft.AspNetCore.Components;

namespace AzureSuite.Web.Blazor.Layout
{
    public partial class MainLayout : LayoutComponentBase
    {
        private AppErrorBoundary? _errorBoundary;

        // The error boundary wraps every page (broad scope), so per Microsoft's guidance it
        // must be explicitly recovered on navigation -- otherwise a page that previously
        // crashed would keep showing its error state even after navigating to an unrelated,
        // perfectly healthy page.
        protected override void OnParametersSet()
        {
            _errorBoundary?.Recover();
        }
    }
}
