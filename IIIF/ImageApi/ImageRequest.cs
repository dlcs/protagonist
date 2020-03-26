using System;

namespace IIIF.ImageApi
{
    /// <summary>
    /// Represents a IIIF image request in format:
    /// {scheme}://{server}{/prefix}/{identifier}/{region}/{size}/{rotation}/{quality}.{format}
    /// </summary>
    /// <remarks>See https://iiif.io/api/image/3.0/#21-image-request-uri-syntax </remarks>
    public class ImageRequest
    {
        public string Prefix { get; set; }
        public string Identifier { get; set; }
        public bool IsBase { get; set; }
        public bool IsInformationRequest { get; set; }
        public RegionParameter Region { get; set; }
        public SizeParameter Size { get; set; }
        public RotationParameter Rotation { get; set; }
        public string Quality { get; set; }
        public string Format { get; set; }
        public string OriginalPath { get; set; }

        public static ImageRequest Parse(string path, string prefix)
        {
            if (path[0] == '/')
            {
                path = path.Substring(1);
            }

            if (prefix.Length > 0)
            {
                if (prefix[0] == '/')
                {
                    prefix = prefix.Substring(1);
                }
                if (prefix != path.Substring(0, prefix.Length))
                {
                    throw new ArgumentException("Path does not start with prefix", nameof(prefix));
                }
                path = path.Substring(prefix.Length);
            }

            var request = new ImageRequest { Prefix = prefix };
            var parts = path.Split('/');
            request.Identifier = parts[0];
            if (parts.Length == 1 || parts[1] == String.Empty)
            {
                // likely the server will want to redirect this
                request.IsBase = true;
                return request;
            }

            if (parts[1] == "info.json")
            {
                request.IsInformationRequest = true;
                return request;
            }

            request.OriginalPath = path;
            request.Region = RegionParameter.Parse(parts[1]);
            request.Size = SizeParameter.Parse(parts[2]);
            request.Rotation = RotationParameter.Parse(parts[3]);
            var filenameParts = parts[4].Split('.');
            request.Quality = filenameParts[0];
            request.Format = filenameParts[1];

            return request;
        }
    }
}
