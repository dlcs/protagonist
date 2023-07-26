using System.Threading;
using DLCS.Core;
using DLCS.Core.Caching;
using DLCS.Model;
using DLCS.Repository.Spaces;
using FakeItEasy;
using LazyCache;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DLCS.Repository.Tests.Spaces;

public class SpaceRepositoryTests
{
    private readonly SpaceRepository sut;
    public SpaceRepositoryTests()
    {
        var dbContext = A.Fake<DlcsContext>();
        var cacheOptions = A.Fake<IOptions<CacheSettings>>();
        var appCache = A.Fake<IAppCache>();
        var entityCounterRepository = A.Fake<IEntityCounterRepository>();


        sut = new SpaceRepository(dbContext, cacheOptions, appCache, new NullLogger<SpaceRepository>(), entityCounterRepository);
    }
    
    [Fact]
    public async Task DeleteSpace_ReturnsError_WhenCalledWithIncorrectDatabaseSetup()
    {
        // Arrange and Act
        var deleteResult = await sut.DeleteSpace(1, 1, new CancellationToken());
        
        // Assert
        deleteResult.Value.Should().Be(DeleteResult.Error);
    }
}