using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Thumbs
{
    public class InfoJsonBuilder
    {
        public static string GetImageApi2_1(string serviceEndpoint, List<int[]> sizes)
        {
            const string template = @"{
""@context"":""http://iiif.io/api/image/2/context.json"",
""@id"":""$id$"",
""protocol"": ""http://iiif.io/api/image"",
""profile"": [
  ""http://iiif.io/api/image/2/level0.json"",
  {
    ""formats"" : [ ""jpg"" ],
    ""qualities"" : [ ""color"" ],
    ""supports"" : [ ""sizeByWhListed"" ]
  }
  ],
  ""width"" : $width$,
  ""height"" : $height$,
  ""sizes"" : [
    $sizes$
  ]
}
";
            return InfoJson(serviceEndpoint, sizes, template);
        }


        public static string GetImageApi3_0(string serviceEndpoint, List<int[]> sizes)
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

        private static string InfoJson(string serviceEndpoint, List<int[]> sizes, string template)
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
                .Replace("$sizes$", sizeStr.ToString());
            
            return infoJson;
        }
    }
}
