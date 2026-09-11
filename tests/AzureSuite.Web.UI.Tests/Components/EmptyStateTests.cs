using AzureSuite.Web.UI.Components;
using Bunit;
using FluentAssertions;

namespace AzureSuite.Web.UI.Tests.Components;

public class EmptyStateTests : BunitContext
{
    [Fact]
    public void RendersMessageInsideEmptyStateDiv()
    {
        var cut = Render<EmptyState>(parameters => parameters
            .Add(p => p.Message, "No message types registered yet."));

        cut.Find(".empty-state").TextContent.Should().Be("No message types registered yet.");
    }
}
