using AzureSuite.Web.Blazor.Layout;
using Bunit;
using FluentAssertions;

namespace AzureSuite.Web.Blazor.Tests.Layout;

public class LoginDisplayTests : BunitContext
{
    [Fact]
    public void SignedIn_ShowsTheNameAndASignOutButton()
    {
        AddAuthorization().SetAuthorized("Oscar Padron");

        var cut = Render<LoginDisplay>();

        cut.Find(".app-nav-user").TextContent.Should().Be("Oscar Padron");
        cut.Find("button").TextContent.Should().Be("sign out");
    }

    [Fact]
    public void SignedOut_ShowsASignInLink()
    {
        AddAuthorization();

        var cut = Render<LoginDisplay>();

        cut.Find("a[href='authentication/login']").TextContent.Should().Be("sign in");
    }
}
