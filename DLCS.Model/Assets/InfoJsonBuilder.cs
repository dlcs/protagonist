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
        /// <param name="sizes">List of sizes image is available in</param>
        /// <param name="width">Width of image</param>
        /// <param name="height">Height of image</param>
        /// <returns>info.json string</returns>
        public static string GetImageApi2_1Level1(string serviceEndpoint, int width, int height, List<int[]> sizes)
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
            return InfoJson(serviceEndpoint, sizes, template, width, height);
        }
        
        public static string GetImageApi2_1Level1Auth(string serviceEndpoint, int width, int height, List<int[]> sizes, string services)
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
  ],
  ""services"": $services$
}
";
            var basicTemplate = InfoJson(serviceEndpoint, sizes, template, width, height);
            return basicTemplate.Replace("$services$", services);
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

        private static string InfoJson(
            string serviceEndpoint, 
            List<int[]> sizes, 
            string template,
            int? width = null,
            int? height = null,
            int tileSize = 256)
        {
            var sizeStr = new StringBuilder();
            int workingWidth = 0;
            int workingHeight = 0;
            foreach (int[] wh in sizes.OrderBy(wh => wh[0]))
            {
                if (workingWidth > 0) sizeStr.Append(", ");
                workingWidth = wh[0];
                workingHeight = wh[1];
                sizeStr.Append("{ \"width\": ");
                sizeStr.Append(workingWidth);
                sizeStr.Append(", \"height\": ");
                sizeStr.Append(workingHeight);
                sizeStr.Append(" }");
            }

            var imgWidth = width ?? workingWidth;
            var imgHeight = height ?? workingHeight;

            var infoJson = template
                .Replace("$id$", serviceEndpoint)
                .Replace("$width$", imgWidth.ToString())
                .Replace("$height$", imgHeight.ToString())
                .Replace("$sizes$", sizeStr.ToString())
                .Replace("$scaleFactors$", GetScaleFactors(imgWidth, imgHeight, tileSize));
            
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
