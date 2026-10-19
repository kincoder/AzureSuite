namespace AzureSuite.Security
{
    /// <summary>Entra ID app role values defined on the MessagingHub-Api registration
    /// (infra/identity/main.bicep). They arrive in the access token's <c>roles</c> claim, both
    /// for signed-in users and for applications using client credentials.</summary>
    public static class AppRoles
    {
        public const string CatalogReader = "Catalog.Reader";
        public const string CatalogAdmin = "Catalog.Admin";
        public const string MessagesSubmit = "Messages.Submit";
    }
}
