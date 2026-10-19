namespace AzureSuite.Web.Blazor.Security
{
    /// <summary>The delegated scope requested for the MessagingHub APIs, and the base URLs that
    /// may receive the resulting token. Tokens are never attached to any other host.</summary>
    public sealed record ApiAccessOptions(string Scope, IReadOnlyList<Uri> AuthorizedUrls);
}
