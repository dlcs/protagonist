using System;
using System.Collections.Generic;
using System.Text;
using IIIF.ImageApi;

namespace DLCS.Model.Assets
{
    public interface IAssetRepository
    {
        public Asset GetAsset(string id);
    }
}
