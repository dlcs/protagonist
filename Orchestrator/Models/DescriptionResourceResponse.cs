namespace Orchestrator.Models
{
    /// <summary>
    /// Represents the results of a call to get a IIIF DescriptionResource (manifest, info.json etc)
    /// </summary>
    public class DescriptionResourceResponse
    {
        // TODO - update this to be a JsonLdBase once all values set use iiif-net nuget package
        public string? DescriptionResource { get; private init; }
        public bool HasResource { get; private init; }
        public bool RequiresAuth { get; private init; }
        public bool IsUnauthorised { get; private init; }

        /// <summary>
        /// Get empty <see cref="DescriptionResourceResponse"/> result, containing no manifest.
        /// </summary>
        public static readonly DescriptionResourceResponse Empty = new();

        /// <summary>
        /// Get <see cref="DescriptionResourceResponse"/> for an open asset
        /// </summary>
        public static DescriptionResourceResponse Open(string resource) 
            => new()
            {
                DescriptionResource = resource,
                RequiresAuth = false,
                HasResource = true,
                IsUnauthorised = false
            };

        /// <summary>
        /// Get <see cref="DescriptionResourceResponse"/> for an restricted asset the user can access
        /// </summary>
        public static DescriptionResourceResponse Restricted(string resource) 
            => new()
            {
                DescriptionResource = resource,
                RequiresAuth = true,
                HasResource = true,
                IsUnauthorised = false
            };
        
        /// <summary>
        /// Get <see cref="DescriptionResourceResponse"/> for an restricted asset the user cannot access
        /// </summary>
        public static DescriptionResourceResponse Unauthorised(string resource) 
            => new()
            {
                DescriptionResource = resource,
                RequiresAuth = true,
                HasResource = true,
                IsUnauthorised = true
            };
    }
}