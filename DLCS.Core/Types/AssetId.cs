using System;
using System.Text;

namespace DLCS.Core.Types
{
    /// <summary>
    /// A record that represents an identifier for a DLCS Asset.
    /// </summary>
    /// <param name="Customer">Id of customer</param>
    /// <param name="Space">Id of space</param>
    /// <param name="Asset">Id of asset</param>
    public record AssetId(int Customer, int Space, string Asset)
    {
        public override string ToString()
        {
            StringBuilder stringBuilder = new();
            PrintMembers(stringBuilder);
            return stringBuilder.ToString();
        }

        /// <summary>
        /// Return a path for use in the private DLCS API (not the IIIF API)
        /// </summary>
        /// <returns></returns>
        public string ToApiResourcePath()
        {
            return $"/customers/{Customer}/spaces/{Space}/images/{Asset}";
        }

        protected virtual bool PrintMembers(StringBuilder builder)
        {
            builder.Append(Customer);
            builder.AppendFormat("/{0}/{1}", Space, Asset);
            return true;
        }

        /// <summary>
        /// Create a new AssetId from string in format customer/space/image
        /// </summary>
        /// <param name="assetImageId">string representing assetImageId</param>
        /// <returns>New <see cref="AssetId"/> record</returns>
        /// <exception cref="FormatException">Thrown if string not in expected format</exception>
        public static AssetId FromString(string assetImageId)
        {
            var parts = assetImageId.Split("/", StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 3)
            {
                throw new FormatException("AssetImageId string must be in format customer/space/image");
            }

            return new AssetId(int.Parse(parts[0]), int.Parse(parts[1]), parts[2]);
        }
    }
}