using AzureSuite.Security;
using AzureSuite.Web.Blazor.Layout;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace AzureSuite.Web.Blazor.Tests.Layout;

public class NavMenuTests : BunitContext
{
    private void SignInWith(params string[] policies)
    {
        var auth = AddAuthorization();
        auth.SetAuthorized("Oscar");
        auth.SetPolicies(policies);
    }

    [Fact]
    public void MarksTheCatalogLinkActiveAtTheAppRoot()
    {
        SignInWith(AppPolicies.CatalogRead, AppPolicies.SubmitMessages);

        var cut = Render<NavMenu>();

        cut.Find("a[href='/']").ClassList.Should().Contain("active");
        cut.Find("a[href='/ingestion']").ClassList.Should().NotContain("active");
    }

    [Fact]
    public void MarksTheIngestionLinkActiveOnTheIngestionRoute()
    {
        SignInWith(AppPolicies.CatalogRead, AppPolicies.SubmitMessages);
        Services.GetRequiredService<NavigationManager>().NavigateTo("/ingestion");

        var cut = Render<NavMenu>();

        cut.Find("a[href='/ingestion']").ClassList.Should().Contain("active");
        cut.Find("a[href='/']").ClassList.Should().NotContain("active");
    }

    [Fact]
    public void HidesCatalogLinksWithoutCatalogRead()
    {
        SignInWith(AppPolicies.SubmitMessages);

        var cut = Render<NavMenu>();

        cut.FindAll("a[href='/catalog/clients']").Should().BeEmpty();
        cut.FindAll("a[href='/catalog/routes']").Should().BeEmpty();
        cut.FindAll("a[href='/ingestion']").Should().ContainSingle();
    }

    [Fact]
    public void HidesTheIngestionLinkWithoutSubmitMessages()
    {
        SignInWith(AppPolicies.CatalogRead);

        var cut = Render<NavMenu>();

        cut.FindAll("a[href='/ingestion']").Should().BeEmpty();
    }
}
