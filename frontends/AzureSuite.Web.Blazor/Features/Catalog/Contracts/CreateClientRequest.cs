namespace AzureSuite.Web.Blazor.Features.Catalog.Contracts
{
    // A mutable class with settable properties, not a positional record -- EditForm's
    // @bind-Value needs a real setter, and a record's init-only accessor can't be assigned
    // outside the constructor (matches RegisterMessageTypeRequest's existing shape).
    public class CreateClientRequest
    {
        public string Name { get; set; } = string.Empty;
    }
}
