using DLCS.Model.Assets;
using DLCS.Repository.Assets;
using DLCS.Repository.Caching;
using LazyCache.Mocks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Test.Helpers.Integration;
using Xunit;

namespace DLCS.Repository.Tests.Assets
{
    [Trait("Category", "Database")]
    [Collection(DatabaseCollection.CollectionName)]
    public class AssetRepositoryTests
    {
        private readonly DlcsContext dbContext;
        private readonly IAssetRepository sut;

        public AssetRepositoryTests(DlcsDatabaseFixture dbFixture)
        {
            dbContext = dbFixture.DbContext;
            sut = new DapperAssetRepository(
                dbFixture.DbContext, 
                null, // TODO
                new MockCachingService(),
                Options.Create(new CacheSettings()),
                new NullLogger<DapperAssetRepository>()
                );

            dbFixture.CleanUp();
        }

    }
}