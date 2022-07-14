using System.Collections.Generic;
using System.Linq;
using DLCS.Core.Collections;
using DLCS.Core.Strings;

namespace DLCS.Core;

/// <summary>
    /// Methods to help with getting file extensions for known content-types.
    /// </summary>
    /// <remarks>This has been copied over from previous solution.</remarks>
    public class MIMEHelper
    {
        /// <summary>
        /// MIME type for jp2 (image/jp2)
        /// </summary>
        public const string JP2 = "image/jp2";
        
        /// <summary>
        /// Alternative MIME type for jp2 (image/jpx)
        /// </summary>
        public const string JPX = "image/jpx";
        
        // Forward = contentType:extension
        // Reverse = extension:contentType
        private static readonly ReadOnlyMap<string, string> ContentTypeExtensionMap =
            new ReadOnlyMap<string, string>(new Dictionary<string, string>
            {
                {"application/pdf", "pdf"},
                {"audio/wav", "wav"},
                {"audio/mp3", "mp3"},
                {"audio/x-mpeg-3", "mp3"},
                {"video/mpeg", "mpg"},
                {"video/mp2", "mp2"},
                {"video/mp4", "mp4"},
                {"image/bmp", "bmp"},
                {"image/cgm", "cgm"},
                {"image/gif", "gif"},
                {"image/ief", "ief"},
                {JP2, "jp2"},
                {JPX, "jp2"},
                {"image/jpeg", "jpg"},
                {"image/jpg", "jpg"}, // common typo that is probably worth supporting even though it's invalid
                {"image/pict", "pic"},
                {"image/png", "png"},
                {"image/svg+xml", "svg"},
                {"image/tiff", "tiff"},
                {"image/tif", "tiff"} // common typo that is probably worth supporting even though it's invalid
            }, true);
        
        /// <summary>
        /// Get file extension for known MIME types.
        /// </summary>
        /// <param name="contentType">ContentType to get extension for.</param>
        /// <returns>Extension, if known.</returns>
        public static string? GetExtensionForContentType(string contentType)
        {
            if (string.IsNullOrWhiteSpace(contentType)) return null;
            
            if (contentType.Contains(";"))
            {
                contentType = contentType.SplitSeparatedString(";").FirstOrDefault();
            }

            return ContentTypeExtensionMap.Forward.TryGetValue(contentType?.ToLower() ?? string.Empty,
                out var extension)
                ? extension
                : null;
        }
        
        /// <summary>
        /// Get MIME types for file extension.
        /// </summary>
        /// <param name="extension">Extension to get content-type for.</param>
        /// <returns>ContentType, if known.</returns>
        public static string? GetContentTypeForExtension(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension)) return null;

            var extensionCandidate = extension.ToLower();
            if (extensionCandidate.StartsWith("."))
            {
                extensionCandidate = extensionCandidate.Replace(".", string.Empty);
            }

            return ContentTypeExtensionMap.Reverse.TryGetValue(extensionCandidate, out var contentType)
                ? contentType
                : null;
        }
    }