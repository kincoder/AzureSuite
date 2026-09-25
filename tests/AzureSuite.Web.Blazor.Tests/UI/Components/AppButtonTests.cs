using AzureSuite.Web.Blazor.UI.Components;
using Bunit;
using FluentAssertions;

namespace AzureSuite.Web.Blazor.Tests.UI.Components;

public class AppButtonTests : BunitContext
{
    [Fact]
    public void RendersButtonElementByDefaultWithPrimaryClass()
    {
        var cut = Render<AppButton>(parameters => parameters
            .AddChildContent("Register"));

        var button = cut.Find("button");
        button.TextContent.Should().Be("Register");
        button.ClassList.Should().Contain("app-button-primary");
        button.GetAttribute("type").Should().Be("button");
    }

    [Fact]
    public void RendersAnchorElementWhenHrefIsSet()
    {
        var cut = Render<AppButton>(parameters => parameters
            .Add(p => p.Href, "/register")
            .AddChildContent("Register a new message type"));

        var anchor = cut.Find("a");
        anchor.GetAttribute("href").Should().Be("/register");
        anchor.TextContent.Should().Be("Register a new message type");
    }

    [Fact]
    public void RendersTargetWithNoopenerWhenTargetIsSet()
    {
        var cut = Render<AppButton>(parameters => parameters
            .Add(p => p.Href, "https://localhost:7184/scalar")
            .Add(p => p.Target, "_blank")
            .AddChildContent("Catalog API"));

        var anchor = cut.Find("a");
        anchor.GetAttribute("target").Should().Be("_blank");
        anchor.GetAttribute("rel").Should().Be("noopener noreferrer");
    }

    [Fact]
    public void OmitsTargetAndRelWhenTargetIsNotSet()
    {
        var cut = Render<AppButton>(parameters => parameters
            .Add(p => p.Href, "/register")
            .AddChildContent("Register"));

        var anchor = cut.Find("a");
        anchor.HasAttribute("target").Should().BeFalse();
        anchor.HasAttribute("rel").Should().BeFalse();
    }

    [Fact]
    public void RendersSecondaryClassWhenVariantIsSecondary()
    {
        var cut = Render<AppButton>(parameters => parameters
            .Add(p => p.Variant, AppButtonVariant.Secondary)
            .AddChildContent("Cancel"));

        cut.Find("button").ClassList.Should().Contain("app-button-secondary");
    }
}
