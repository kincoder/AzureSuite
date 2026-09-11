using AzureSuite.Catalog.Application.MessageTypes.Commands.RegisterMessageType;
using Catalog.Application.Tests.TestDoubles;
using FluentAssertions;
using Xunit;

namespace Catalog.Application.Tests.MessageTypes.Commands.RegisterMessageType;

public class RegisterMessageTypeHandlerTests
{
    [Fact]
    public async Task Handle_WithNewMessageType_RegistersAndReturnsDto()
    {
        var repository = new FakeMessageTypeRepository();
        var handler = new RegisterMessageTypeHandler(repository);
        var command = new RegisterMessageTypeCommand("pacs.008", "1.0", "{}");

        var result = await handler.Handle(command, CancellationToken.None);

        result.Name.Should().Be("pacs.008");
        result.Version.Should().Be("1.0");
        result.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handle_WithDuplicateNameAndVersion_ThrowsInvalidOperationException()
    {
        var repository = new FakeMessageTypeRepository();
        var handler = new RegisterMessageTypeHandler(repository);
        var command = new RegisterMessageTypeCommand("pacs.008", "1.0", "{}");
        await handler.Handle(command, CancellationToken.None);

        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
