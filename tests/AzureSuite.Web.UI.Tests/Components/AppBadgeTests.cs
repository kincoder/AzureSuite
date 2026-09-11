using AzureSuite.Web.UI.Components;
using Bunit;
using FluentAssertions;

namespace AzureSuite.Web.UI.Tests.Components;

public class AppBadgeTests : BunitContext
{
    [Fact]
    public void RendersChildContentInsideBadgeSpan()
    {
        var cut = Render<AppBadge>(parameters => parameters
            .AddChildContent("1.0"));

        cut.Find(".app-badge").TextContent.Should().Be("1.0");
    }
}
