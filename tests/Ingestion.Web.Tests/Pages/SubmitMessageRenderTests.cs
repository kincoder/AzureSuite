using AzureSuite.Ingestion.Web.Pages;
using AzureSuite.Ingestion.Web.Services;
using AzureSuite.Web.UI;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AzureSuite.Ingestion.Web.Tests.Pages;

public class SubmitMessageRenderTests : BunitContext
{
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
}
