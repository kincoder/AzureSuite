namespace AzureSuite.Catalog.Web.Contracts
{
    /// <summary>Request body sent to Catalog.Api's POST /message-types endpoint.</summary>
    public class RegisterMessageTypeRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string SchemaDefinition { get; set; } = string.Empty;
    }
}
