using AzureSuite.Catalog.Application.MessageTypes.Queries.ListMessageTypes;
using AzureSuite.Catalog.Domain.Entities;
using AzureSuite.Catalog.Domain.ValueObjects;
using Catalog.Application.Tests.TestDoubles;
using FluentAssertions;
using Xunit;

namespace Catalog.Application.Tests.MessageTypes.Queries.ListMessageTypes;

public class ListMessageTypesHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsAllRegisteredMessageTypesAsDtos()
    {
        var repository = new FakeMessageTypeRepository();
        await repository.AddAsync(new MessageType(new MessageTypeName("pacs.008"), new MessageTypeVersion("1.0"), "{}"), CancellationToken.None);
        await repository.AddAsync(new MessageType(new MessageTypeName("camt.054"), new MessageTypeVersion("1.0"), "{}"), CancellationToken.None);
        var handler = new ListMessageTypesHandler(repository);

        var result = await handler.Handle(new ListMessageTypesQuery(), CancellationToken.None);

        result.Should().HaveCount(2);
        result.Select(m => m.Name).Should().BeEquivalentTo("pacs.008", "camt.054");
    }
}
