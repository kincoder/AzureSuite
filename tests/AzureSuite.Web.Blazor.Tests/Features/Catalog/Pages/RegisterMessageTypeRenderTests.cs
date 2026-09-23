using System.Net;
using AzureSuite.Web.Blazor.Features.Catalog.Pages;
using AzureSuite.Web.Blazor.Features.Catalog.Services;
using AzureSuite.Web.Blazor.UI;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AzureSuite.Web.Blazor.Tests.Features.Catalog.Pages;

public class RegisterMessageTypeRenderTests : BunitContext
{
    private sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }

    public RegisterMessageTypeRenderTests()
    {
        Services.AddScoped(_ => new CatalogApiClient(new HttpClient { BaseAddress = new Uri("https://localhost/") }));
        Services.AddScoped(_ => new ClientTelemetryLogger(JSInterop.JSRuntime));
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

    [Fact]
    public void RendersBackToListLinkInPageHeader()
    {
        var cut = Render<RegisterMessageType>();

        var backLink = cut.Find(".page-header a");
        backLink.GetAttribute("href").Should().Be("/");
        backLink.TextContent.Should().Be("Back to list");
    }

    [Fact]
    public void ClearsAPastErrorBannerAsSoonAsTheUserEditsAFieldAgain()
    {
        Services.AddScoped(_ => new CatalogApiClient(new HttpClient(
            new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Conflict)))
        { BaseAddress = new Uri("https://localhost/") }));
        JSInterop.SetupVoid("logException", _ => true);

        var cut = Render<RegisterMessageType>();
        cut.Find("input").Change("camt.054");
        cut.FindAll("textarea")[0].Change("{}");
        cut.Find("button[type=submit]").Click();

        cut.Find(".error-state").Should().NotBeNull();

        cut.Find("input").Change("camt.055");

        cut.FindAll(".error-state").Should().BeEmpty();
    }
}
