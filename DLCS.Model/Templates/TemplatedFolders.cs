using System.IO;
using DLCS.Core.Types;

namespace DLCS.Model.Templates
{
    /// <summary>
    /// Collection of methods for dealing with folder templates for saving assets to local disk.
    /// </summary>
    /// <remarks>This logic has been copied over from Deliverator implementation.</remarks> 
    public static class TemplatedFolders
    {
        private const string Root = "{root}";
        private const string Customer = "{customer}";
        private const string Space = "{space}";
        private const string Image = "{image}";

        /// <summary>
        /// Generate a folder template using provided details.
        /// </summary>
        /// <param name="template">The basic template, e.g. {root}\{customer}\{space}\{image}</param>
        /// <param name="root">The root of the template, used as {root} param.</param>
        /// <param name="asset">Used to populate {customer}, {space} and, optionally, {image} properties.</param>
        /// <param name="replaceImage">If true {image} is replaced, else it is left untouched</param>
        /// <returns>New string with replacements made.</returns>
        public static string GenerateTemplate(string template, string root, AssetId asset, bool replaceImage = true)
        {
            var replacements = template
                .Replace(Root, root)
                .Replace(Customer, asset.Customer.ToString())
                .Replace(Space, asset.Space.ToString());

            return replaceImage
                ? replacements.Replace(Image, SplitImageNameToFolders(asset.Asset, Path.DirectorySeparatorChar))
                : replacements;
        }

        private static string SplitImageNameToFolders(string name, char separator)
            => name.Length <= 8
                ? name
                : string.Concat(
                    name[..2], separator,
                    name[2..4], separator,
                    name[4..6], separator,
                    name[6..8], separator,
                    name);
    }
}