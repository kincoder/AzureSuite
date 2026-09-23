namespace AzureSuite.Catalog.Application.MessageTypes.Queries.ValidateMessage
{
    public record ValidationResultDto(bool IsValid, IReadOnlyList<string> Errors);
}
