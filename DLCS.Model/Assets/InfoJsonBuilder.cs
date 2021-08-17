using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DLCS.Model.Assets
{
    /// <summary>
    /// Contains methods for building info.json responses.
    /// </summary>
    public static class InfoJsonBuilder
    {
        /// <summary>
        /// Get level 0 info.json object
        /// </summary>
        /// <param name="serviceEndpoint">URI for image</param>
        /// <param name="sizes">List of sizes image is available in.</param>
        /// <returns>info.json string</returns>
        public static string GetImageApi2_1Level0(string serviceEndpoint, List<int[]> sizes)
        {
            const string template = @"{
""@context"":""http://iiif.io/api/image/2/context.json"",
""@id"":""$id$"",
""protocol"": ""http://iiif.io/api/image"",
""profile"": [
  ""http://iiif.io/api/image/2/level0.json"",
  {
    ""formats"": [ ""jpg"" ],
    ""qualities"": [ ""color"" ],
    ""supports"": [ ""sizeByWhListed"" ]
  }
  ],
  ""width"": $width$,
  ""height"": $height$,
  ""sizes"": [
    $sizes$
  ]
}
";
            return InfoJson(serviceEndpoint, sizes, template);
        }
        
        /// <summary>
        /// Get full info.json for use by image-services
        /// </summary>
        /// <param name="serviceEndpoint">URI for image</param>
        /// <param name="sizes">List of sizes image is available in.</param>
        /// <returns>info.json string</returns>
        public static string GetImageApi2_1Level1(string serviceEndpoint, List<int[]> sizes)
        {
            const string template = @"{
""@context"":""http://iiif.io/api/image/2/context.json"",
""@id"":""$id$"",
""protocol"": ""http://iiif.io/api/image"",
""profile"": [
  ""http://iiif.io/api/image/2/level1.json"",
  {
    ""formats"": [ ""jpg"" ],
    ""qualities"": [ ""native"",""color"",""gray"" ],
    ""supports"": [ ""regionByPct"",""sizeByForcedWh"",""sizeByWh"",""sizeAboveFull"",""rotationBy90s"",""mirroring"",""gray"" ]
  }
  ],
  ""width"": $width$,
  ""height"": $height$,
  ""tiles"": [
    { ""width"": 256, ""height"": 256, ""scaleFactors"": [ $scaleFactors$ ] }
  ],
  ""sizes"": [
    $sizes$
  ]
}
";
            return InfoJson(serviceEndpoint, sizes, template);
        }

        public static string GetThumbsImageApi3_0(string serviceEndpoint, List<int[]> sizes)
        {
            const string template = @"{
  ""@context"": [
    ""http://iiif.io/api/image/3/context.json""
  ],
  ""id"": ""$id"",
  ""type"": ""ImageService3"",
  ""protocol"": ""http://iiif.io/api/image"",
  ""profile"": ""level0"",
  ""width"":  $width$,
  ""height"": $height$,
  ""sizes"": [
    $sizes$
  ],
  ""extraFeatures"": [ ""sizeByWhListed"", ""profileLinkHeader"" ]
}
";
            return InfoJson(serviceEndpoint, sizes, template);
        }

        private static string InfoJson(string serviceEndpoint, List<int[]> sizes, string template, int tileSize = 256)
        {
            var sizeStr = new StringBuilder();
            int width = 0;
            int height = 0;
            foreach (int[] wh in sizes.OrderBy(wh => wh[0]))
            {
                if (width > 0) sizeStr.Append(", ");
                width = wh[0];
                height = wh[1];
                sizeStr.Append("{ \"width\": ");
                sizeStr.Append(width);
                sizeStr.Append(", \"height\": ");
                sizeStr.Append(height);
                sizeStr.Append(" }");
            }

            var infoJson = template
                .Replace("$id$", serviceEndpoint)
                .Replace("$width$", width.ToString())
                .Replace("$height$", height.ToString())
                .Replace("$sizes$", sizeStr.ToString())
                .Replace("$scaleFactors$", GetScaleFactors(width, height, tileSize));
            
            return infoJson;
        }

        private static string GetScaleFactors(int width, int height, int tileSize)
        {
            var max = Math.Max(width, height);
            var factors = new List<int> { 1 };
            while (max > tileSize)
            {
                max /= 2;
                factors.Add(factors[^1] * 2);
            }
            return string.Join(", ", factors);
        }
    }
}
