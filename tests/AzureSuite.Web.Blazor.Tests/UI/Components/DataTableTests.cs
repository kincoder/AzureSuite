using AzureSuite.Web.Blazor.UI.Components;
using Bunit;
using FluentAssertions;

namespace AzureSuite.Web.Blazor.Tests.UI.Components;

public class DataTableTests : BunitContext
{
    private sealed record Row(string Name);

    [Fact]
    public void RendersHeaderAndOneRowPerItem()
    {
        var cut = Render<DataTable<Row>>(parameters => parameters
            .Add(p => p.Items, new[] { new Row("pacs.008"), new Row("camt.054") })
            .Add(p => p.HeaderContent, builder => builder.AddMarkupContent(0, "<th>Name</th>"))
            .Add(p => p.RowTemplate, (Row row) => (builder =>
            {
                builder.OpenElement(0, "td");
                builder.AddContent(1, row.Name);
                builder.CloseElement();
            })));

        cut.Find("thead").TextContent.Should().Be("Name");
        cut.FindAll("tbody tr").Should().HaveCount(2);
        cut.FindAll("tbody tr")[0].TextContent.Should().Be("pacs.008");
        cut.FindAll("tbody tr")[1].TextContent.Should().Be("camt.054");
    }
}
