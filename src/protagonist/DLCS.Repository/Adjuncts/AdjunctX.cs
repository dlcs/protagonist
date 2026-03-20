using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using Microsoft.EntityFrameworkCore;

namespace DLCS.Repository.Adjuncts;

public static class AdjunctX
{
    public static async Task<Adjunct?> GetAdjunct(this DbSet<Adjunct> adjuncts, string adjunctId, AssetId assetId, CancellationToken cancellationToken) =>
        await adjuncts.SingleOrDefaultAsync(a => a.Id == adjunctId && a.AssetId == assetId, cancellationToken);

}
