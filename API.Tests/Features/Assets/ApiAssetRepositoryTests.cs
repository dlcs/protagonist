using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using API.Features.Assets;
using API.Tests.Integration.Infrastructure;
using DLCS.Model.Assets;
using DLCS.Repository;
using DLCS.Repository.Assets;
using DLCS.Repository.Caching;
using FluentAssertions;
using LazyCache.Mocks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Test.Helpers.Integration;
using Xunit;

namespace API.Tests.Features.Assets
{
    [Trait("Category", "Database")]
    [Collection(CollectionDefinitions.DatabaseCollection.CollectionName)]
    public class AssetRepositoryTests
    {
        private readonly DlcsContext dbContext;
        private readonly ApiAssetRepository sut;

        public AssetRepositoryTests(DlcsDatabaseFixture dbFixture)
        {
            // We use a customised dbcontext because we want different tracking behaviour
            dbContext = new DlcsContext(
                new DbContextOptionsBuilder<DlcsContext>()
                    .UseNpgsql(dbFixture.ConnectionString).Options
            );
            // We want this turned on to match live behaviour
            dbContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new[]
                    { new KeyValuePair<string, string>("ConnectionStrings:PostgreSQLConnection", dbFixture.ConnectionString) })
                .Build();
            var dapperAssetRepository = new DapperAssetRepository(
                config,
                new MockCachingService(),
                Options.Create(new CacheSettings()),
                new NullLogger<DapperAssetRepository>()
            );

            sut = new ApiAssetRepository(dbContext, config, dapperAssetRepository);

            dbFixture.CleanUp();
        }

        [Fact]
        public async Task AssetRepository_Saves_New_Asset()
        {
            var id = "new-asset";
            var newAsset = new Asset { Id = id, Customer = 100, Space = 10, Reference1 = "I am new", 
                Origin = "https://example.org/image1.tiff"};
        
            var result = AssetPreparer.PrepareAssetForUpsert(null, newAsset, false);
            result.Success.Should().BeTrue();

            await sut.Save(newAsset, CancellationToken.None);

            var dbAsset = await dbContext.Images.FindAsync(id);
            dbAsset.Reference1.Should().Be("I am new");
            dbAsset.Reference2.Should().Be("");
            dbAsset.MediaType.Should().Be("unknown");
        }
        
        [Fact]
        public async Task AssetRepository_Saves_Existing_Asset()
        {
            var id = "existing-asset";
            // previously
            var assetEntry = await dbContext.Images.AddTestAsset(id, ref1:"I am original 1", ref2:"I am original 2");
            await dbContext.SaveChangesAsync();
            assetEntry.State = EntityState.Detached;
        
            var existingAsset = await dbContext.Images.AsNoTracking().FirstAsync(a => a.Id == id);
            var patch = new Asset { Id = id };
            // change something:
            patch.Reference1 = "I am changed";
        
            var result = AssetPreparer.PrepareAssetForUpsert(existingAsset, patch, false);
            result.Success.Should().BeTrue();
        
            await sut.Save(patch, CancellationToken.None);

            var dbAsset = await dbContext.Images.FindAsync(id);
            dbAsset.Reference1.Should().Be("I am changed");
            dbAsset.Reference2.Should().Be("I am original 2");
        }

        [Fact]
        public async Task AssetRepository_Saves_Tracked_Asset()
        {
            var id = "tracked-asset";
            // previously
            var assetEntry = await dbContext.Images.AddTestAsset(id, ref1: "I am original 1", ref2: "I am original 2",
                origin: "https://example.org/image1.tiff");
            await dbContext.SaveChangesAsync();

            var trackedAsset = assetEntry.Entity;
            trackedAsset.Reference1 = "I am changed";
        
            var result = AssetPreparer.PrepareAssetForUpsert(null, trackedAsset,
                true); // <-- note this is true for a tracked asset...
            result.Success.Should().BeTrue();
        
            await sut.Save(trackedAsset, CancellationToken.None);

            var dbAsset = await dbContext.Images.FindAsync(id);
            dbAsset.Reference1.Should().Be("I am changed");
            dbAsset.Reference2.Should().Be("I am original 2");
        }

        [Fact]
        public async Task AssetRepository_Throws_if_Same_Asset_Tracked()
        {
            var id = "tracked-asset-same";
            // previously
            await dbContext.Images.AddTestAsset(id, ref1:"I am original 1", ref2:"I am original 2");
            await dbContext.SaveChangesAsync();

            var sameAsset = new Asset { Id = id, Reference1 = "I am changed" };
        
            AssetPreparer.PrepareAssetForUpsert(null, sameAsset,
                true); // <-- note this is true for a tracked asset...
        
            Func<Task> save = () => sut.Save(sameAsset, CancellationToken.None); 
            await save.Should().ThrowAsync<InvalidOperationException>();
        }
    }
}