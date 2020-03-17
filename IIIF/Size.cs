using System;
using Newtonsoft.Json;

namespace IIIF
{
    /// <summary>
    /// Represents the 2d size of an object.
    /// </summary>
    public class Size
    {
        [JsonProperty(PropertyName = "width")]
        public int Width { get; private set; }
        
        [JsonProperty(PropertyName = "height")]
        public int Height { get; private set; }

        [JsonIgnore] public int MaxDimension => Width > Height ? Width : Height;
        
        public override string ToString() => $"{Width},{Height}";

        /// <summary>
        /// Create new Size object with specified width and height.
        /// </summary>
        public Size(int width, int height)
        {
            Width = width;
            Height = height;
        }
        
        /// <summary>
        /// Get size object as w,h array
        /// </summary>
        /// <returns></returns>
        public int[] ToArray() => new[] {Width, Height};

        /// <summary>
        /// Checks if current Size is confined within specified size.
        /// </summary>
        /// <param name="confineSize">Size object to check if confined within.</param>
        /// <returns>true if current item would fit inside specified size; else false.</returns>
        public bool IsConfinedWithin(Size confineSize)
            => Width <= confineSize.Width && Height <= confineSize.Height;

        /// <summary>
        /// Create new Size object representing square.
        /// </summary>
        /// <param name="dimension">width and height of square</param>
        public static Size Square(int dimension)
            => new Size(dimension, dimension);

        /// <summary>
        /// Create new Size object from "w,h" array.
        /// </summary>
        /// <param name="size">w,h array</param>
        /// <returns>New Size object</returns>
        public static Size FromArray(int[] size)
            => new Size
            (
                size[0],
                size[1]
            );

        /// <summary>
        /// Confine specified Size object to bounding square of specified size.
        /// </summary>
        /// <param name="boundingSquare">Dimension of bounding square to confine object to.</param>
        /// <param name="imageSize">Size object to Confine dimensions to.</param>
        /// <returns>New <see cref="Size"/> object with dimensions bound to specified square.</returns>
        public static Size Confine(int boundingSquare, Size imageSize)
            => Confine(Size.Square(boundingSquare), imageSize);

        /// <summary>
        /// Confine specified Size object to bounding square of specified size.
        /// </summary>
        /// <param name="requiredSize">Dimension of bounding square to confine object to.</param>
        /// <param name="imageSize">Size object to Confine dimensions to.</param>
        /// <returns>New <see cref="Size"/> object with dimensions bound to specified square.</returns>
        public static Size Confine(Size requiredSize, Size imageSize)
        {
            if (imageSize.Width <= requiredSize.Width && imageSize.Height <= requiredSize.Height)
            {
                return imageSize;
            }
            var scaleW = requiredSize.Width / (double)imageSize.Width;
            var scaleH = requiredSize.Height / (double)imageSize.Height;
            var scale = Math.Min(scaleW, scaleH);
            return new Size(
                (int) Math.Round(imageSize.Width * scale),
                (int) Math.Round(imageSize.Height * scale)
            );
        }
    }
}