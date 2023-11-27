using System.Threading;
using DLCS.Core;
using DLCS.Core.Caching;
using DLCS.Core.Types;
using DLCS.Model;
using DLCS.Repository.Spaces;
using FakeItEasy;
using LazyCache.Mocks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Test.Helpers.Integration;

namespace DLCS.Repository.Tests.Spaces;

[Trait("Category", "Database")]
[Collection(DatabaseCollection.CollectionName)]
public class SpaceRepositoryTests
{
    private readonly DlcsContext dbContext;
    private readonly SpaceRepository sut;
    private readonly MockCachingService appCache;
    
    public SpaceRepositoryTests(DlcsDatabaseFixture dbFixture)
    {
        dbContext = dbFixture.DbContext;
        var cacheOptions = A.Fake<IOptions<CacheSettings>>();
        appCache = new MockCachingService();
        var entityCounterRepository = A.Fake<IEntityCounterRepository>();


        sut = new SpaceRepository(dbFixture.DbContext, Options.Create(new CacheSettings()), appCache, new NullLogger<SpaceRepository>(), entityCounterRepository);

        dbFixture.CleanUp();
        dbContext.Images.AddTestAsset(AssetId.FromString("99/1/1"), ref1: "foobar");
        dbContext.SaveChanges();
    }
    
    [Fact]
    public async Task DeleteSpace_ReturnsError_WhenCalledWithIncorrectDatabaseSetup()
    {
        var sutTwo = new SpaceRepository(A.Fake<DlcsContext>(), 
            Options.Create(new CacheSettings()), appCache, new NullLogger<SpaceRepository>(), 
            A.Fake<IEntityCounterRepository>());
        
        // Arrange and Act
        var deleteResult = await sutTwo.DeleteSpace(1, 1, new CancellationToken());
        
        // Assert
        deleteResult.Value.Should().Be(DeleteResult.Error);
    }
    
    [Fact]
    public async Task GetSpace_ReturnsSpace_WhenCalled()
    {
        // Arrange and Act
        var getResult = await sut.GetSpace(99, 1, new CancellationToken());

        // Assert
        getResult.Customer.Should().Be(99);
        getResult.Name.Should().Be("space-1");
    }
    
    [Fact]
    public async Task GetSpaceWithName_ReturnsSpace_WhenCalled()
    {
        // Arrange and Act
        var getResult = await sut.GetSpace(99, "space-1", new CancellationToken());

        // Assert
        getResult.Customer.Should().Be(99);
        getResult.Id.Should().Be(1);
    }
}