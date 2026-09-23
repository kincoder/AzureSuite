using AzureSuite.Web.Blazor.UI.Components;
using Bunit;
using FluentAssertions;

namespace AzureSuite.Web.Blazor.Tests.UI.Components;

public class SkeletonRowTests : BunitContext
{
    [Fact]
    public void RendersOneSkeletonCellPerColumn()
    {
        var cut = Render<SkeletonRow>(parameters => parameters
            .Add(p => p.ColumnCount, 3));

        cut.FindAll("td").Should().HaveCount(3);
        cut.FindAll(".skeleton-line").Should().HaveCount(3);
    }
}
