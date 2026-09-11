using AzureSuite.Web.UI.Components;
using Bunit;
using FluentAssertions;

namespace AzureSuite.Web.UI.Tests.Components;

public class SkeletonCardTests : BunitContext
{
    [Fact]
    public void RendersThreeShimmerLinesInsideSkeletonCard()
    {
        var cut = Render<SkeletonCard>();

        cut.Find(".skeleton-card").QuerySelectorAll(".skeleton-line").Should().HaveCount(3);
    }
}
