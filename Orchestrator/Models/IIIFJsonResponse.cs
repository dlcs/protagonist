namespace Orchestrator.Models
{
    public class IIIFJsonResponse
    {
        // TODO - use JsonLdBase, rather than string
        public string? InfoJson { get; private init; }
        public bool HasInfoJson { get; private init; }
        public bool RequiresAuth { get; private init; }
        public bool IsUnauthorised { get; private init; }

        /// <summary>
        /// Get empty <see cref="IIIFJsonResponse"/> result, containing no manifest.
        /// </summary>
        public static readonly IIIFJsonResponse Empty = new();

        /// <summary>
        /// Get <see cref="IIIFJsonResponse"/> for an open asset
        /// </summary>
        public static IIIFJsonResponse Open(string infoJson) 
            => new()
            {
                InfoJson = infoJson,
                RequiresAuth = false,
                HasInfoJson = true,
                IsUnauthorised = false
            };

        /// <summary>
        /// Get <see cref="IIIFJsonResponse"/> for an restricted asset the user can access
        /// </summary>
        public static IIIFJsonResponse Restricted(string infoJson) 
            => new()
            {
                InfoJson = infoJson,
                RequiresAuth = true,
                HasInfoJson = true,
                IsUnauthorised = false
            };
        
        /// <summary>
        /// Get <see cref="IIIFJsonResponse"/> for an restricted asset the user cannot access
        /// </summary>
        public static IIIFJsonResponse Unauthorised(string infoJson) 
            => new()
            {
                InfoJson = infoJson,
                RequiresAuth = true,
                HasInfoJson = true,
                IsUnauthorised = true
            };
    }
}