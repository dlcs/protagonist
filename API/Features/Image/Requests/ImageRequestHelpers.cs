using System.Threading;
using System.Threading.Tasks;
using DLCS.Model.Assets;
using DLCS.Repository;

namespace API.Features.Image.Requests
{
    
    public class ImageRequestHelpers
    {
        public static async Task<Asset> GetImageInternal(DlcsContext dbContext, string key, CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }
    }
}