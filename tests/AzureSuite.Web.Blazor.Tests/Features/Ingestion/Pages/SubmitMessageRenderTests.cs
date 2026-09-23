using System.Net;
using AzureSuite.Web.Blazor.Features.Ingestion.Pages;
using AzureSuite.Web.Blazor.Features.Ingestion.Services;
using AzureSuite.Web.Blazor.UI;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AzureSuite.Web.Blazor.Tests.Features.Ingestion.Pages;

public class SubmitMessageRenderTests : BunitContext
{
    private sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }

    public SubmitMessageRenderTests()
    {
        Services.AddScoped(_ => new IngestionApiClient(new HttpClient { BaseAddress = new Uri("https://localhost/") }));
        Services.AddScoped(_ => new ClientTelemetryLogger(JSInterop.JSRuntime));
    }

    [Fact]
    public void RendersThreeFormGroupsAndAPrimarySubmitButton()
    {
        var cut = Render<SubmitMessage>();

        cut.FindAll(".form-group").Should().HaveCount(3);
        var submit = cut.Find("button[type=submit]");
        submit.ClassList.Should().Contain("app-button-primary");
        submit.TextContent.Should().Be("Submit");
    }

    [Fact]
    public void ClearsAPastErrorBannerAsSoonAsTheUserEditsAFieldAgain()
    {
        Services.AddScoped(_ => new IngestionApiClient(new HttpClient(
            new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)))
        { BaseAddress = new Uri("https://localhost/") }));
        JSInterop.SetupVoid("logException", _ => true);

        var cut = Render<SubmitMessage>();
        cut.Find("input").Change("pacs.008");
        cut.Find("textarea").Change("{}");
        cut.Find("button[type=submit]").Click();

        cut.Find(".error-state").Should().NotBeNull();

        cut.Find("input").Change("pacs.009");

        cut.FindAll(".error-state").Should().BeEmpty();
    }
}
