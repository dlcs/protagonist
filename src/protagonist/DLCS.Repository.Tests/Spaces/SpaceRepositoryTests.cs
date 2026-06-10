using System;
using System.Threading;
using DLCS.Core;
using DLCS.Core.Caching;
using DLCS.Core.Types;
using DLCS.Model;
using DLCS.Model.Spaces;
using DLCS.Model.Storage;
using DLCS.Repository.Entities;
using DLCS.Repository.Spaces;
using FakeItEasy;
using LazyCache.Mocks;
using Microsoft.EntityFrameworkCore;
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
    private readonly IEntityCounterRepository entityCounterRepository;
    private readonly IStorageRepository storageRepository;

    public SpaceRepositoryTests(DlcsDatabaseFixture dbFixture)
    {
        dbContext = dbFixture.DbContext;
        // Create a dbContext for use in tests - this has change tracking etc like the normal injected context
        var dlcsContext = new DlcsContext(
            new DbContextOptionsBuilder<DlcsContext>()
                .UseNpgsql(dbFixture.ConnectionString).Options
        );
        appCache = new MockCachingService();
        entityCounterRepository = A.Fake<IEntityCounterRepository>();
        storageRepository = A.Fake<IStorageRepository>();

        sut = new SpaceRepository(dlcsContext, Options.Create(new CacheSettings()), appCache,
            entityCounterRepository, storageRepository, new NullLogger<SpaceRepository>());

        dbFixture.CleanUp();
    }
    
    [Fact]
    public async Task DeleteSpace_ReturnsError_WhenCalledWithIncorrectDatabaseSetup()
    {
        var sutTwo = new SpaceRepository(A.Fake<DlcsContext>(), Options.Create(new CacheSettings()), appCache,
            A.Fake<IEntityCounterRepository>(), A.Fake<IStorageRepository>(), new NullLogger<SpaceRepository>());
        
        // Arrange and Act
        var deleteResult = await sutTwo.DeleteSpace(1, 1, CancellationToken.None);
        
        // Assert
        deleteResult.Value.Should().Be(DeleteResult.Error);
    }
    
    [Fact]
    public async Task GetSpace_ReturnsSpace_WhenCalled()
    {
        // Arrange and Act
        var getResult = await sut.GetSpace(99, 1, CancellationToken.None);

        // Assert
        getResult.Customer.Should().Be(99);
        getResult.Name.Should().Be("space-1");
    }
    
    [Fact]
    public async Task GetSpaceWithName_ReturnsSpace_WhenCalled()
    {
        // Arrange and Act
        var getResult = await sut.GetSpace(99, "space-1", CancellationToken.None);

        // Assert
        getResult.Customer.Should().Be(99);
        getResult.Id.Should().Be(1);
    }

    [Fact]
    public async Task DeleteSpace_ReturnsNotFound_WhenSpaceDoesNotExist()
    {
        // Act
        var deleteResult = await sut.DeleteSpace(99, 999, CancellationToken.None);

        // Assert
        deleteResult.Value.Should().Be(DeleteResult.NotFound);
    }

    [Fact]
    public async Task DeleteSpace_ReturnsConflict_WhenSpaceHasImages()
    {
        await dbContext.Images.AddTestAsset(AssetId.FromString("99/1/1"), ref1: "foobar");
        await dbContext.SaveChangesAsync();
        var deleteResult = await sut.DeleteSpace(99, 1, CancellationToken.None);

        // Assert
        deleteResult.Value.Should().Be(DeleteResult.Conflict);
    }

    [Fact]
    public async Task DeleteSpace_ReturnsDeleted_AndCallsCleanup_WhenSuccessful()
    {
        // Arrange
        const int customerId = 99;
        const int spaceId = 2;
        dbContext.Spaces.Add(new Space
        {
            Id = spaceId, Customer = customerId, Name = "space-to-delete", Created = DateTime.UtcNow,
            ImageBucket = string.Empty, Tags = [], Roles = [],
            MaxUnauthorised = -1
        });
        await dbContext.SaveChangesAsync();

        // Act
        var deleteResult = await sut.DeleteSpace(customerId, spaceId, CancellationToken.None);

        // Assert
        deleteResult.Value.Should().Be(DeleteResult.Deleted);
        A.CallTo(() => storageRepository.DeleteCustomerStorage(customerId, spaceId, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => entityCounterRepository.Decrement(customerId, KnownEntityCounters.CustomerSpaces, "99", 1))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => entityCounterRepository.Remove(customerId, KnownEntityCounters.SpaceImages, "2", 1))
            .MustHaveHappenedOnceExactly();
    }
}
