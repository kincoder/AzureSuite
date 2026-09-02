using AzureSuite.Application.Abstractions;
using AzureSuite.Domain.Entities;
using AzureSuite.Domain.ValueObjects;

namespace AzureSuite.Application.Messages;

public class Pacs008MessageService(IPacs008MessageRepository repository) : IPacs008MessageService
{
    public async Task<IReadOnlyList<Pacs008MessageSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var messages = await repository.GetAllAsync(cancellationToken);

        return messages.Select(ToSummaryDto).ToList();
    }

    public async Task<Pacs008MessageSummaryDto> CreateAsync(CreatePacs008MessageRequest request, CancellationToken cancellationToken = default)
    {
        var message = new Pacs008Message
        {
            MessageId = request.MessageId,
            EndToEndId = request.EndToEndId,
            Amount = request.Amount,
            Currency = request.Currency,
            CreationDateTime = DateTimeOffset.UtcNow,
            Debtor = new PartyAccount { Name = request.DebtorName, Iban = request.DebtorIban, BicCode = request.DebtorBic },
            Creditor = new PartyAccount { Name = request.CreditorName, Iban = request.CreditorIban, BicCode = request.CreditorBic },
            RemittanceInformation = request.RemittanceInformation
        };

        await repository.AddAsync(message, cancellationToken);

        return ToSummaryDto(message);
    }

    private static Pacs008MessageSummaryDto ToSummaryDto(Pacs008Message message) => new(
        message.Id,
        message.MessageId,
        message.EndToEndId,
        message.Amount,
        message.Currency,
        message.Status.ToString());
}
