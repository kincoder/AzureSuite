namespace AzureSuite.Security
{
    /// <summary>Named authorization policies shared by the APIs (enforcement) and the web app
    /// (deciding what to show).</summary>
    public static class AppPolicies
    {
        public const string CatalogRead = "CatalogRead";
        public const string CatalogWrite = "CatalogWrite";
        public const string SubmitMessages = "SubmitMessages";
    }
}
