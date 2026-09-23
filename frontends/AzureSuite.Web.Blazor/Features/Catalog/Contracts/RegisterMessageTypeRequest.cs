namespace AzureSuite.Web.Blazor.Features.Catalog.Contracts
{
    public class RegisterMessageTypeRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string SchemaDefinition { get; set; } = string.Empty;
    }
}
