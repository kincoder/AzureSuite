using AzureSuite.Web.UI.Components;
using Bunit;
using FluentAssertions;

namespace AzureSuite.Web.UI.Tests.Components;

public class PageHeaderTests : BunitContext
{
    [Fact]
    public void RendersTitleEyebrowAndSubtitle()
    {
        var cut = Render<PageHeader>(parameters => parameters
            .Add(p => p.Title, "Registered Message Types")
            .Add(p => p.Eyebrow, "Catalog")
            .Add(p => p.Subtitle, "All message types currently registered"));

        cut.Find("h1").TextContent.Should().Be("Registered Message Types");
        cut.Find(".eyebrow").TextContent.Should().Be("Catalog");
        cut.Find(".subtitle").TextContent.Should().Be("All message types currently registered");
    }

    [Fact]
    public void RendersActionsFragmentWhenProvided()
    {
        var cut = Render<PageHeader>(parameters => parameters
            .Add(p => p.Title, "Register Message Type")
            .Add(p => p.Actions, "<button>Go</button>"));

        cut.Find("button").TextContent.Should().Be("Go");
    }
}
