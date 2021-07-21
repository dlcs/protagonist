using System;
using System.Text;

namespace DLCS.Core.Types
{
    /// <summary>
    /// A record that represents an identifier for a DLCS Asset.
    /// </summary>
    /// <param name="Customer">Id of customer</param>
    /// <param name="Space">Id of space</param>
    /// <param name="Image">Id of image</param>
    public record AssetImageId(int Customer, int Space, string Image)
    {
        public override string ToString()
        {
            StringBuilder stringBuilder = new();
            PrintMembers(stringBuilder);
            return stringBuilder.ToString();
        }

        protected virtual bool PrintMembers(StringBuilder builder)
        {
            builder.Append(Customer);
            builder.AppendFormat("/{0}/{1}", Space, Image);
            return true;
        }

        /// <summary>
        /// Create a new AssetImageId from string in format customer/space/image
        /// </summary>
        /// <param name="assetImageId">string representing assetImageId</param>
        /// <returns>New <see cref="AssetImageId"/> record</returns>
        /// <exception cref="FormatException">Thrown if string not in expected format</exception>
        public static AssetImageId FromString(string assetImageId)
        {
            var parts = assetImageId.Split("/", StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 3)
            {
                throw new FormatException("AssetImageId string must be in format customer/space/image");
            }

            return new AssetImageId(int.Parse(parts[0]), int.Parse(parts[1]), parts[2]);
        }
    }
}