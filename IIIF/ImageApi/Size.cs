using System;
using System.Text;
using Newtonsoft.Json;

namespace IIIF.ImageApi
{
    public class Size
    {
        [JsonProperty(Order = 35, PropertyName = "width")]
        public int Width { get; set; }

        [JsonProperty(Order = 36, PropertyName = "height")]
        public int Height { get; set; }

        [JsonIgnore]
        public bool Max { get; set; }

        [JsonIgnore]
        public bool Upscaled { get; set; }

        [JsonIgnore]
        public bool Confined { get; set; }

        [JsonIgnore]
        public float PercentScale { get; set; }


        public override string ToString()
        {
            return Width + "," + Height;
        }

        public string ToPathPartString()
        {
            var sb = new StringBuilder();
            if (Upscaled)
            {
                sb.Append('^');
            }
            if (Max)
            {
                sb.Append("max");
                return sb.ToString();
            }
            if (Confined)
            {
                sb.Append('!');
            }
            if (PercentScale > 0)
            {
                sb.Append("pct:" + PercentScale);
                return sb.ToString();
            }
            if (Width > 0)
            {
                sb.Append(Width);
            }
            sb.Append(',');
            if (Height > 0)
            {
                sb.Append(Height);
            }

            return sb.ToString();
        }

        public static Size Parse(string pathPart)
        {
            var size = new Size();
            if (pathPart[0] == '^')
            {
                size.Upscaled = true;
                pathPart = pathPart.Substring(1);
            }

            if (pathPart == "max" || pathPart == "full")
            {
                size.Max = true;
                return size;
            }

            if (pathPart[0] == '!')
            {
                size.Confined = true;
                pathPart = pathPart.Substring(1);
            }

            if (pathPart[0] == 'p')
            {
                size.PercentScale = float.Parse(pathPart.Substring(4));
                return size;
            }

            string[] wh = pathPart.Split(',');
            if (wh[0] != String.Empty)
            {
                size.Width = int.Parse(wh[0]);
            }
            if (wh[1] != String.Empty)
            {
                size.Height = int.Parse(wh[1]);
            }

            return size;
        }

        public int[] ToArray()
        {
            return new[] {Width, Height};
        }

        public static Size FromArray(int[] size)
        {
            return new Size
            {
                Width = size[0],
                Height = size[1]
            };
        }

        public static Size Confine(int boundingSquare, Size imageSize)
        {
            return Confine(new Size { Width = boundingSquare, Height = boundingSquare }, imageSize);
        }

        public static Size Confine(Size requiredSize, Size imageSize)
        {
            if (imageSize.Width <= requiredSize.Width && imageSize.Height <= requiredSize.Height)
            {
                return imageSize;
            }
            var scaleW = requiredSize.Width / (double)imageSize.Width;
            var scaleH = requiredSize.Height / (double)imageSize.Height;
            var scale = Math.Min(scaleW, scaleH);
            return new Size
            {
                Width = (int)Math.Round((imageSize.Width * scale)),
                Height = (int)Math.Round((imageSize.Height * scale))
            };
        }
    }
}
