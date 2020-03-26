using System;
using Newtonsoft.Json;

namespace IIIF.ImageApi
{
    /// <summary>
    /// Represents the {region} parameter of a IIIF image request.
    /// </summary>
    /// <remarks>see https://iiif.io/api/image/3.0/#41-region </remarks>
    public class RegionParameter
    {
        [JsonProperty(Order = 91, PropertyName = "x")]
        public float X { get; set; }

        [JsonProperty(Order = 92, PropertyName = "y")]
        public float Y { get; set; }

        [JsonProperty(Order = 93, PropertyName = "w")]
        public float W { get; set; }

        [JsonProperty(Order = 94, PropertyName = "h")]
        public float H { get; set; }

        public bool Full { get; set; }
        public bool Square { get; set; }
        public bool Percent { get; set; }


        public override string ToString()
        {
            if (Full) return "full";
            if (Square) return "square";
            string xywh = $"{X},{Y},{W},{H}";
            if (Percent) return $"pct:{xywh}";
            return xywh;
        }

        public static RegionParameter Parse(string pathPart)
        {
            try
            {
                if (pathPart == "full")
                {
                    return new RegionParameter { Full = true };
                }
                if (pathPart == "square")
                {
                    return new RegionParameter { Square = true };
                }

                bool percent = pathPart.StartsWith("pct:");
                var stringParts = pathPart.Substring(percent ? 4 : 0).Split(',');
                var xywh = Array.ConvertAll(stringParts, float.Parse);
                return new RegionParameter
                {
                    X = xywh[0], Y = xywh[1], W = xywh[2], H = xywh[3], 
                    Percent = percent
                };
            }
            catch
            {
                throw new ArgumentException("Expected 'full', 'square' or 'x,y,w,h'. Found " + pathPart);
            }
        }
    }
}
