using AzureSuite.Application.Messages;
using AzureSuite.Application.Tests.TestDoubles;
using AzureSuite.Domain.Enums;
using FluentAssertions;

namespace AzureSuite.Application.Tests.Messages;

public class Pacs008MessageServiceTests
{
    private static CreatePacs008MessageRequest CreateRequest() => new()
    {
        MessageId = "MSG-0001",
        EndToEndId = "E2E-0001",
        Amount = 100.50m,
        Currency = "EUR",
        DebtorName = "Alice",
        DebtorIban = "DE89370400440532013000",
        DebtorBic = "COBADEFFXXX",
        CreditorName = "Bob",
        CreditorIban = "FR1420041010050500013M02606",
        CreditorBic = "PSSTFRPPXXX",
        RemittanceInformation = "Invoice 42"
    };

    [Fact]
    public async Task CreateAsync_PersistsMessage_ViaRepository()
    {
        var repository = new FakePacs008MessageRepository();
        var service = new Pacs008MessageService(repository);

        await service.CreateAsync(CreateRequest());

        repository.AddedMessages.Should().ContainSingle();
        repository.AddedMessages[0].MessageId.Should().Be("MSG-0001");
    }

    [Fact]
    public async Task CreateAsync_ReturnsDto_WithStatusReceived()
    {
        var repository = new FakePacs008MessageRepository();
        var service = new Pacs008MessageService(repository);

        var result = await service.CreateAsync(CreateRequest());

        result.Status.Should().Be(nameof(MessageStatus.Received));
        result.Amount.Should().Be(100.50m);
        result.Currency.Should().Be("EUR");
    }

    [Fact]
    public async Task CreateAsync_MapsDebtorAndCreditor_OntoTheDomainEntity()
    {
        var repository = new FakePacs008MessageRepository();
        var service = new Pacs008MessageService(repository);

        await service.CreateAsync(CreateRequest());

        var stored = repository.AddedMessages[0];
        stored.Debtor.Name.Should().Be("Alice");
        stored.Creditor.Name.Should().Be("Bob");
    }

    [Fact]
    public async Task GetAllAsync_ReturnsEmptyList_WhenNoMessagesExist()
    {
        var repository = new FakePacs008MessageRepository();
        var service = new Pacs008MessageService(repository);

        var result = await service.GetAllAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllPersistedMessages_AsSummaryDtos()
    {
        var repository = new FakePacs008MessageRepository();
        var service = new Pacs008MessageService(repository);
        await service.CreateAsync(CreateRequest());

        var result = await service.GetAllAsync();

        result.Should().ContainSingle(m => m.MessageId == "MSG-0001");
    }
}
