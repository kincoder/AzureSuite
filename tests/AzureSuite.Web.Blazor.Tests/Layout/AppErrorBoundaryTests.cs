using AzureSuite.Web.Blazor.Layout;
using AzureSuite.Web.Blazor.UI;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace AzureSuite.Web.Blazor.Tests.Layout;

public class AppErrorBoundaryTests : BunitContext
{
    private sealed class ThrowingComponent : ComponentBase
    {
        protected override void OnInitialized() => throw new InvalidOperationException("boom");
    }

    public AppErrorBoundaryTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddScoped(_ => new ClientTelemetryLogger(JSInterop.JSRuntime));
    }

    [Fact]
    public void ShowsAFriendlyMessageInsteadOfCrashingWhenAChildComponentThrows()
    {
        var cut = Render<AppErrorBoundary>(parameters => parameters
            .AddChildContent<ThrowingComponent>());

        cut.Find(".error-state").TextContent.Should().Contain("Something went wrong");
    }
}
