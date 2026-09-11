using AzureSuite.Catalog.Web.Pages;
using AzureSuite.Catalog.Web.Services;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AzureSuite.Catalog.Web.Tests.Pages;

public class RegisterMessageTypeRenderTests : BunitContext
{
    public RegisterMessageTypeRenderTests()
    {
        Services.AddScoped(_ => new CatalogApiClient(new HttpClient { BaseAddress = new Uri("https://localhost/") }));
    }

    [Fact]
    public void RendersThreeFormGroupsAndAPrimarySubmitButton()
    {
        var cut = Render<RegisterMessageType>();

        cut.FindAll(".form-group").Should().HaveCount(3);
        var submit = cut.Find("button[type=submit]");
        submit.ClassList.Should().Contain("app-button-primary");
        submit.TextContent.Should().Be("Register");
    }
}
