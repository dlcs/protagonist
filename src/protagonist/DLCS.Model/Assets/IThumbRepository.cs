using System.Collections.Generic;
using System.Threading.Tasks;
using DLCS.Core.Types;

namespace DLCS.Model.Assets;

public interface IThumbRepository
{
    /// <summary>
    /// Get a list of all open thumbnails for specified image.
    /// </summary>
    Task<List<int[]>?> GetOpenSizes(AssetId assetId);
}
