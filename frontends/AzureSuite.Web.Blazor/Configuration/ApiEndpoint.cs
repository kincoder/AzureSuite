namespace AzureSuite.Web.Blazor.Configuration
{
    /// <summary>A backend API this app calls, identified by the base address its typed HttpClient uses.</summary>
    public record ApiEndpoint(string Name, Uri BaseAddress)
    {
        /// <summary>The API's Scalar reference page, which the APIs map at /scalar when they run in Development.</summary>
        public Uri ReferenceUrl => new(BaseAddress, "scalar");
    }

    /// <summary>The APIs the footer links to. The footer renders nothing when the list is empty.</summary>
    public record ApiReferenceLinks(IReadOnlyList<ApiEndpoint> Apis);
}
