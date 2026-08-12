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
        getResult!.Customer.Should().Be(99);
        getResult.Name.Should().Be("space-1");
    }
    
    [Fact]
    public async Task GetSpaceWithName_ReturnsSpace_WhenCalled()
    {
        // Arrange and Act
        var getResult = await sut.GetSpace(99, "space-1", CancellationToken.None);

        // Assert
        getResult!.Customer.Should().Be(99);
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
        const int customerId = 9903;
        await dbContext.Images.AddTestAsset(AssetId.FromString("9903/1/1"), customer: customerId);
        await dbContext.Spaces.AddTestSpace(customerId, 1);
        await dbContext.SaveChangesAsync();
        var deleteResult = await sut.DeleteSpace(customerId, 1, CancellationToken.None);

        // Assert
        deleteResult.Value.Should().Be(DeleteResult.Conflict);
    }

    [Fact]
    public async Task DeleteSpace_ReturnsDeleted_AndCallsCleanup_WhenSuccessful()
    {
        // Arrange
        const int customerId = 9901;
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
        A.CallTo(() => entityCounterRepository.Decrement(customerId, KnownEntityCounters.CustomerSpaces, "9901", 1))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => entityCounterRepository.Remove(customerId, KnownEntityCounters.SpaceImages, "2", 1))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task CreateSpace_ReturnsSpace_WithProvidedValues()
    {
        // Arrange
        const int customer = 192239;
        A.CallTo(() => entityCounterRepository.GetNext(customer, KnownEntityCounters.CustomerSpaces, customer.ToString(), 1))
            .Returns(2L);

        // Act
        var space = await sut.CreateSpace(customer, "new-space", "my-bucket", ["tag1"], ["role1"],
            CancellationToken.None);

        // Assert
        space.Id.Should().Be(2);
        space.Customer.Should().Be(customer);
        space.Name.Should().Be("new-space");
        space.ImageBucket.Should().Be("my-bucket");
        space.Tags.Should().BeEquivalentTo(["tag1"]);
        space.Roles.Should().BeEquivalentTo(["role1"]);
        space.MaxUnauthorised.Should().Be(-1);
    }

    [Fact]
    public async Task CreateSpace_ReturnsSpace_WithDefaultValues_WhenOptionalParamsNull()
    {
        // Arrange
        const int customer = 123929;
        A.CallTo(() => entityCounterRepository.GetNext(customer, KnownEntityCounters.CustomerSpaces, customer.ToString(), 1))
            .Returns(2L);

        // Act
        var space = await sut.CreateSpace(customer, "new-space", null, null, null, CancellationToken.None);

        // Assert
        space.ImageBucket.Should().BeEmpty();
        space.Tags.Should().BeEmpty();
        space.Roles.Should().BeEmpty();
        space.MaxUnauthorised.Should().Be(-1);
    }

    [Fact]
    public async Task CreateSpace_CreatesSpaceImagesCounter()
    {
        // Arrange
        const int customer = 1929;
        A.CallTo(() => entityCounterRepository.GetNext(customer, KnownEntityCounters.CustomerSpaces, customer.ToString(), 1))
            .Returns(2L);

        // Act
        await sut.CreateSpace(customer, "new-space", null, null, null, CancellationToken.None);

        // Assert
        A.CallTo(() => entityCounterRepository.TryCreate(customer, KnownEntityCounters.SpaceImages, "2", 1))
            .MustHaveHappenedOnceExactly();
    }
    
    [Fact]
    public async Task CreateSpace_CreatesCustomerStorage()
    {
        // Arrange
        const int customer = 199;
        A.CallTo(() => entityCounterRepository.GetNext(customer, KnownEntityCounters.CustomerSpaces, customer.ToString(), 1))
            .Returns(2L);

        // Act
        await sut.CreateSpace(customer, "new-space", null, null, null, CancellationToken.None);

        // Assert
        A.CallTo(() => storageRepository.TryCreateCustomerStorage(customer, 2, "default", CancellationToken.None))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task CreateSpace_SkipsId_IfIdAlreadyInUse()
    {
        // Arrange - space 1 already exists; first call returns 1, second returns 2
        const int customer = 13299;
        dbContext.Spaces.Add(new Space { Id = 1, Customer = customer, Name = "space-already" });
        await dbContext.SaveChangesAsync();
        A.CallTo(() => entityCounterRepository.GetNext(customer, KnownEntityCounters.CustomerSpaces, customer.ToString(), 1))
            .Returns(1L).Once()
            .Then.Returns(2L);

        // Act
        var space = await sut.CreateSpace(customer, "new-space", null, null, null, CancellationToken.None);

        // Assert
        space.Id.Should().Be(2);
        A.CallTo(() =>
                entityCounterRepository.GetNext(customer, KnownEntityCounters.CustomerSpaces, customer.ToString(), 1))
            .MustHaveHappenedTwiceExactly();
    }
}
