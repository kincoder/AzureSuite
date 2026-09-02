namespace AzureSuite.Application.Messages;

public interface IPacs008MessageService
{
    Task<IReadOnlyList<Pacs008MessageSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Pacs008MessageSummaryDto> CreateAsync(CreatePacs008MessageRequest request, CancellationToken cancellationToken = default);
}
