using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Net.Http.Headers;
using Presi = IIIF.Presentation;

namespace Orchestrator.Infrastructure.IIIF.Conneg
{
    public static class IIIFPresentation
    {
        /// <summary>
        /// Get <see cref="IIIF.Presentation.Version"/> for provided mediaTypeHeaders, favouring latest version.
        /// </summary>
        /// <param name="mediaTypeHeaders">Collection of <see cref="MediaTypeHeaderValue"/> objects.</param>
        /// <param name="fallbackVersion">Value to return if no specific version found.</param>
        /// <returns>IIIF version derived from provided values.</returns>
        public static Presi.Version GetIIIFPresentationType(
            this IEnumerable<MediaTypeHeaderValue> mediaTypeHeaders,
            Presi.Version fallbackVersion = Presi.Version.Unknown)
        {
            var mediaTypes = mediaTypeHeaders ?? Enumerable.Empty<MediaTypeHeaderValue>();

            // Get a list of all "profile" parameters, ordered to prefer most recent.
            var profiles = mediaTypes
                .Select(m =>
                    m.Parameters.SingleOrDefault(p =>
                        string.Equals(p.Name.Value, "profile", StringComparison.OrdinalIgnoreCase))?.Value.Value)
                .OrderByDescending(s => s);

            var v3Profile = $"\"{Presi.Context.Presentation3Context}\"";
            var v2Profile = $"\"{Presi.Context.Presentation2Context}\"";

            foreach (var profile in profiles)
            {
                if (string.IsNullOrEmpty(profile)) continue;
                if (profile == v3Profile)
                {
                    return Presi.Version.V3;
                }

                if (profile == v2Profile)
                {
                    return Presi.Version.V2;
                }
            }

            return fallbackVersion;
        }
    }
}