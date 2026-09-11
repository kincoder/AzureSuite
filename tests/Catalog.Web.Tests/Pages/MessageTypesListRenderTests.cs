using AzureSuite.Catalog.Web.Pages;
using AzureSuite.Catalog.Web.Services;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AzureSuite.Catalog.Web.Tests.Pages;

public class MessageTypesListRenderTests : BunitContext
{
    public MessageTypesListRenderTests()
    {
        Services.AddScoped(_ => new CatalogApiClient(new HttpClient { BaseAddress = new Uri("https://localhost/") }));
    }

    [Fact]
    public void RendersSixSkeletonRowsWhileLoading()
    {
        // CatalogApiClient's GetMessageTypesAsync call against a fake base address never
        // resolves within the render window, so the component stays in its loading state.
        var cut = Render<MessageTypesList>();

        cut.Find(".data-table tbody").QuerySelectorAll("tr").Should().HaveCount(6);
        cut.FindAll(".skeleton-line").Should().HaveCount(18);
    }
}
