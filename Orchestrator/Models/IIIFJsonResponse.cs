namespace Orchestrator.Models
{
    public class IIIFJsonResponse
    {
        // TODO - use JsonLdBase, rather than string
        public string? InfoJson { get; private init; }
        public bool HasInfoJson { get; private init; }
        public bool RequiresAuth { get; private init; }

        public static IIIFJsonResponse Empty = new();

        public static IIIFJsonResponse Open(string infoJson) 
            => new()
            {
                InfoJson = infoJson,
                RequiresAuth = false,
                HasInfoJson = true
            };

        public static IIIFJsonResponse Restricted(string infoJson) 
            => new()
            {
                InfoJson = infoJson,
                RequiresAuth = true,
                HasInfoJson = true
            };
    }
}