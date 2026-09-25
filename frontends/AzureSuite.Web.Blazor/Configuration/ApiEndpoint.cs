namespace AzureSuite.Web.Blazor.Configuration
{
    /// <summary>A backend API this app calls, identified by the base address its typed HttpClient uses.</summary>
    public record ApiEndpoint(string Name, Uri BaseAddress)
    {
        /// <summary>The API's Scalar reference page, which the APIs map at /scalar in Development.</summary>
        public Uri ReferenceUrl => new(BaseAddress, "scalar");
    }

    /// <summary>The APIs to link to from the footer. Empty outside Development, where the APIs
    /// don't serve their Scalar pages.</summary>
    public record ApiReferenceLinks(IReadOnlyList<ApiEndpoint> Apis);
}
