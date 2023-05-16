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
    
    /// <summary>
    /// Get a list of all available thumbnails for specified image, regardless of whether open or auth
    /// </summary>
    Task<List<int[]>?> GetAllSizes(AssetId assetId);
}
