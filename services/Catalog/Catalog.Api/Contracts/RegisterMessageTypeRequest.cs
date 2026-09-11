namespace AzureSuite.Catalog.Api.Contracts
{
    /// <summary>Request body for registering a new message type via <c>POST /message-types</c>.</summary>
    public record RegisterMessageTypeRequest(string Name, string Version, string SchemaDefinition);
}
