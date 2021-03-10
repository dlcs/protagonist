using System.Collections.Generic;

namespace DLCS.Web.Response
{
    /// <summary>
    /// A collection of options related to path generation.
    /// </summary>
    public class PathTemplateOptions
    {
        /// <summary>
        /// Default path template if no overrides found.
        /// </summary>
        public string Default { get; set; } = "/{prefix}/{customer}/{space}/{assetPath}";

        /// <summary>
        /// Collection of path template overrides, keyed by hostname.
        /// </summary>
        public Dictionary<string, string> Overrides { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Get template path for host. 
        /// </summary>
        /// <param name="host">Host to get template path for.</param>
        /// <returns>Returns path for host, or default if override not found.</returns>
        public string GetPathTemplateForHost(string host)
            => Overrides.TryGetValue(host, out var template) ? template : Default;
    }
}