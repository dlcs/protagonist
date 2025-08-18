#nullable disable
using DLCS.Core.Types;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DLCS.Repository;

/// <summary>
/// <see cref="ValueConverter{TModel,TProvider}"/> for converting <see cref="AssetId"/> to/from string in db
/// </summary>
internal class AssetIdConverter : ValueConverter<AssetId, string>
{
    public AssetIdConverter()
        : base(
            assetId => assetId.ToString(),
            db => AssetId.FromString(db))
    {
    }
}