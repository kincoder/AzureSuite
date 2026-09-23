using AzureSuite.Web.Blazor.Layout;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace AzureSuite.Web.Blazor.Tests.Layout;

public class NavMenuTests : BunitContext
{
    [Fact]
    public void MarksTheCatalogLinkActiveAtTheAppRoot()
    {
        var cut = Render<NavMenu>();

        cut.Find("a[href='/']").ClassList.Should().Contain("active");
        cut.Find("a[href='/ingestion']").ClassList.Should().NotContain("active");
    }

    [Fact]
    public void MarksTheIngestionLinkActiveOnTheIngestionRoute()
    {
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/ingestion");

        var cut = Render<NavMenu>();

        cut.Find("a[href='/ingestion']").ClassList.Should().Contain("active");
        cut.Find("a[href='/']").ClassList.Should().NotContain("active");
    }
}
