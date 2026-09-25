namespace AzureSuite.Catalog.Application.Common
{
    /// <summary>Thrown when a command refers to another entity (e.g. a Route's Client or
    /// MessageType) that does not exist. The API maps this to 400 Bad Request.</summary>
    public class ReferencedEntityNotFoundException : Exception
    {
        public ReferencedEntityNotFoundException(string message) : base(message)
        {
        }
    }
}
