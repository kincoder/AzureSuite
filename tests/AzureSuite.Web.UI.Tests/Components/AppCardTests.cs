using AzureSuite.Web.UI.Components;
using Bunit;
using FluentAssertions;

namespace AzureSuite.Web.UI.Tests.Components;

public class AppCardTests : BunitContext
{
    [Fact]
    public void RendersChildContentInsideCardDiv()
    {
        var cut = Render<AppCard>(parameters => parameters
            .AddChildContent("<p>pacs.008</p>"));

        var card = cut.Find(".app-card");
        card.QuerySelector("p")!.TextContent.Should().Be("pacs.008");
    }
}
