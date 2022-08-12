using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Caching;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using FakeItEasy;
using FluentAssertions;
using LazyCache;
using LazyCache.Mocks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orchestrator.Assets;
using Orchestrator.Features.Images.Orchestration.Status;
using Xunit;

namespace Orchestrator.Tests.Assets;

public class MemoryAssetTrackerTests
{
    private readonly IAssetRepository assetRepository;
    private readonly IThumbRepository thumbRepository;
    private readonly MemoryAssetTracker sut;
    private readonly IImageOrchestrationStatusProvider imageOrchestrationStatusProvider;

    public MemoryAssetTrackerTests()
    {
        assetRepository = A.Fake<IAssetRepository>();
        thumbRepository = A.Fake<IThumbRepository>();
        imageOrchestrationStatusProvider = A.Fake<IImageOrchestrationStatusProvider>();

        sut = GetSut();
    }

    private MemoryAssetTracker GetSut(IAppCache appCache = null)
    {
        return new MemoryAssetTracker(assetRepository, appCache ?? new MockCachingService(), thumbRepository,
            imageOrchestrationStatusProvider, Options.Create(new CacheSettings()),
            new NullLogger<MemoryAssetTracker>());
    }

    [Fact]
    public async Task GetOrchestrationAsset_Null_IfNotFound()
    {
        // Arrange
        var assetId = new AssetId(1, 1, "go!");
        A.CallTo(() => assetRepository.GetAsset(assetId)).Returns<Asset>(null);
        
        // Act
        var result = await sut.GetOrchestrationAsset(assetId);
        
        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData(AssetFamily.Image, typeof(OrchestrationImage))]
    [InlineData(AssetFamily.Timebased, typeof(OrchestrationAsset))]
    [InlineData(AssetFamily.File, typeof(OrchestrationFile))]
    public async Task GetOrchestrationAsset_ReturnsCorrectType(AssetFamily family, Type expectedType)
    {
        // Arrange
        var assetId = new AssetId(1, 1, "go!");
        A.CallTo(() => assetRepository.GetAsset(assetId)).Returns(new Asset { Family = family });
        
        // Act
        var result = await sut.GetOrchestrationAsset(assetId);
        
        // Assert
        result.AssetId.Should().Be(assetId);
        result.Should().BeOfType(expectedType);
    }
    
    [Theory]
    [InlineData(AssetFamily.Image)]
    [InlineData(AssetFamily.Timebased)]
    [InlineData(AssetFamily.File)]
    public async Task GetOrchestrationAsset_Null_IfAssetFoundButNotForDelivery(AssetFamily family)
    {
        // Arrange
        var assetId = new AssetId(1, 1, "go!");
        A.CallTo(() => assetRepository.GetAsset(assetId))
            .Returns(new Asset { Family = family, NotForDelivery = true });
        
        // Act
        var result = await sut.GetOrchestrationAsset(assetId);
        
        // Assert
        result.Should().BeNull();
    }
    
    [Fact]
    public async Task GetOrchestrationAssetT_Null_IfOrchestrationAssetNotFound()
    {
        // Arrange
        var assetId = new AssetId(1, 1, "go!");
        A.CallTo(() => assetRepository.GetAsset(assetId)).Returns<Asset>(null);
        
        // Act
        var result = await sut.GetOrchestrationAsset<OrchestrationAsset>(assetId);
        
        // Assert
        result.Should().BeNull();
    }
    
    [Fact]
    public async Task GetOrchestrationAssetT_Null_IfOrchestrationImageNotFound()
    {
        // Arrange
        var assetId = new AssetId(1, 1, "go!");
        A.CallTo(() => assetRepository.GetAsset(assetId)).Returns<Asset>(null);
        
        // Act
        var result = await sut.GetOrchestrationAsset<OrchestrationImage>(assetId);
        
        // Assert
        result.Should().BeNull();
    }
    
    [Fact]
    public async Task GetOrchestrationAssetT_Null_IfAssetFoundButNotForDelivery()
    {
        // Arrange
        var assetId = new AssetId(1, 1, "go!");
        A.CallTo(() => assetRepository.GetAsset(assetId)).Returns(new Asset { NotForDelivery = true });
        
        // Act
        var result = await sut.GetOrchestrationAsset<OrchestrationImage>(assetId);
        
        // Assert
        result.Should().BeNull();
    }
    
    [Theory]
    [InlineData(AssetFamily.Timebased)]
    [InlineData(AssetFamily.File)]
    public async Task GetOrchestrationAssetT_ReturnsOrchestrationAsset(AssetFamily family)
    {
        // Arrange
        var assetId = new AssetId(1, 1, "go!");
        A.CallTo(() => assetRepository.GetAsset(assetId)).Returns(new Asset { Family = family });
        
        // Act
        var result = await sut.GetOrchestrationAsset<OrchestrationAsset>(assetId);
        
        // Assert
        result.AssetId.Should().Be(assetId);
        A.CallTo(() => thumbRepository.GetOpenSizes(A<AssetId>._)).MustNotHaveHappened();
    }
    
    [Fact]
    public async Task GetOrchestrationAssetT_ReturnsOrchestrationImage()
    {
        // Arrange
        var assetId = new AssetId(1, 1, "go!");
        var sizes = new List<int[]> { new[] { 100, 200 } };
        A.CallTo(() => assetRepository.GetAsset(assetId)).Returns(new Asset
        {
            Family = AssetFamily.Image, Height = 10, Width = 50, MaxUnauthorised = -1
        });
        A.CallTo(() => thumbRepository.GetOpenSizes(assetId)).Returns(sizes);

        // Act
        var result = await sut.GetOrchestrationAsset<OrchestrationImage>(assetId);
        
        // Assert
        result.Version.Should().Be(0);
        result.AssetId.Should().Be(assetId);
        result.Height.Should().Be(10);
        result.Width.Should().Be(50);
        result.MaxUnauthorised.Should().Be(-1);
        result.OpenThumbs.Should().BeEquivalentTo(sizes);
        result.Status.Should().Be(OrchestrationStatus.Unknown);
        A.CallTo(() => imageOrchestrationStatusProvider.GetOrchestrationStatus(assetId))
            .MustNotHaveHappened();
    }
    
    [Fact]
    public async Task GetOrchestrationAssetT_RequireOrchestrationStatusTrue_ReturnsOrchestrationImage()
    {
        // Arrange
        var assetId = new AssetId(1, 1, "go!");
        var sizes = new List<int[]> { new[] { 100, 200 } };
        A.CallTo(() => assetRepository.GetAsset(assetId)).Returns(new Asset
        {
            Family = AssetFamily.Image, Height = 10, Width = 50, MaxUnauthorised = -1
        });
        A.CallTo(() => thumbRepository.GetOpenSizes(assetId)).Returns(sizes);
        A.CallTo(() => imageOrchestrationStatusProvider.GetOrchestrationStatus(assetId))
            .Returns(OrchestrationStatus.Orchestrating);

        // Act
        var result = await sut.GetOrchestrationAsset<OrchestrationImage>(assetId, true);
        
        // Assert
        result.Version.Should().Be(0);
        result.AssetId.Should().Be(assetId);
        result.Height.Should().Be(10);
        result.Width.Should().Be(50);
        result.MaxUnauthorised.Should().Be(-1);
        result.OpenThumbs.Should().BeEquivalentTo(sizes);
        result.Status.Should().Be(OrchestrationStatus.Orchestrating);
    }
    
    [Theory]
    [InlineData(AssetFamily.Timebased)]
    [InlineData(AssetFamily.File)]
    public async Task GetOrchestrationAssetT_Null_IfWrongTypeAskedFor(AssetFamily family)
    {
        // Arrange
        var assetId = new AssetId(1, 1, "go!");
        A.CallTo(() => assetRepository.GetAsset(assetId)).Returns(new Asset { Family = family });
        
        // Act
        var result = await sut.GetOrchestrationAsset<OrchestrationImage>(assetId);
        
        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData("", -1, false)]
    [InlineData("", 0, true)]
    [InlineData("", 10, true)]
    [InlineData("role", -1, true)]
    [InlineData("role", 0, true)]
    [InlineData("role", 10, true)]
    public async Task GetOrchestrationAsset_SetsRequiresAuthCorrectly(string roles, int maxUnauth, bool requiresAuth)
    {
        // Arrange
        var assetId = new AssetId(1, 1, "go!");
        A.CallTo(() => assetRepository.GetAsset(assetId)).Returns(new Asset
        {
            Family = AssetFamily.Image, MaxUnauthorised = maxUnauth, Roles = roles
        });
        
        // Act
        var result = await sut.GetOrchestrationAsset<OrchestrationImage>(assetId);
        
        // Assert
        result.RequiresAuth.Should().Be(requiresAuth);
    }

    [Fact]
    public async Task RefreshCachedAsset_Version0_IfNotInCache()
    {
        // Arrange
        var assetId = new AssetId(1, 1, "go!");
        var sizes = new List<int[]> { new[] { 100, 200 } };
        A.CallTo(() => assetRepository.GetAsset(assetId)).Returns(new Asset
        {
            Family = AssetFamily.Image, Height = 10, Width = 50, MaxUnauthorised = -1
        });
        A.CallTo(() => thumbRepository.GetOpenSizes(assetId)).Returns(sizes);
        
        // Act
        var result = await sut.RefreshCachedAsset<OrchestrationImage>(assetId);
        
        // Assert
        result.Version.Should().Be(0);
        result.AssetId.Should().Be(assetId);
        result.Height.Should().Be(10);
        result.Width.Should().Be(50);
        result.MaxUnauthorised.Should().Be(-1);
        result.OpenThumbs.Should().BeEquivalentTo(sizes);
        result.Status.Should().Be(OrchestrationStatus.Unknown);
        A.CallTo(() => imageOrchestrationStatusProvider.GetOrchestrationStatus(assetId)).MustNotHaveHappened();
    }
    
    [Fact]
    public async Task RefreshCachedAsset_VersionIncremented_IfAlreadyCache()
    {
        // Arrange
        var assetId = new AssetId(1, 1, "go!");
        var sizes = new List<int[]> { new[] { 100, 200 } };
        A.CallTo(() => assetRepository.GetAsset(assetId)).Returns(new Asset
        {
            Family = AssetFamily.Image, Height = 10, Width = 50, MaxUnauthorised = -1
        });
        A.CallTo(() => thumbRepository.GetOpenSizes(assetId)).Returns(sizes);
        var cache = new DictionaryCache();
        cache.Add("Track:1/1/go!", new OrchestrationImage { Version = 10, AssetId = assetId });
        
        // Act
        var localSut = GetSut(cache);
        var result = await localSut.RefreshCachedAsset<OrchestrationImage>(assetId);
        
        // Assert
        result.Version.Should().Be(11);
        result.AssetId.Should().Be(assetId);
        result.Height.Should().Be(10);
        result.Width.Should().Be(50);
        result.MaxUnauthorised.Should().Be(-1);
        result.OpenThumbs.Should().BeEquivalentTo(sizes);
        result.Status.Should().Be(OrchestrationStatus.Unknown);
        A.CallTo(() => imageOrchestrationStatusProvider.GetOrchestrationStatus(assetId)).MustNotHaveHappened();
    }
    
    [Fact]
    public async Task RefreshCachedAsset_RequireOrchestrationStatusTrue_SetsStatus()
    {
        // Arrange
        var assetId = new AssetId(1, 1, "go!");
        var sizes = new List<int[]> { new[] { 100, 200 } };
        A.CallTo(() => assetRepository.GetAsset(assetId)).Returns(new Asset
        {
            Family = AssetFamily.Image, Height = 10, Width = 50, MaxUnauthorised = -1
        });
        A.CallTo(() => thumbRepository.GetOpenSizes(assetId)).Returns(sizes);
        A.CallTo(() => imageOrchestrationStatusProvider.GetOrchestrationStatus(assetId))
            .Returns(OrchestrationStatus.Orchestrating);
        var cache = new DictionaryCache();
        cache.Add("Track:1/1/go!",
            new OrchestrationImage { Version = 10, AssetId = assetId, Status = OrchestrationStatus.Unknown });
        
        // Act
        var localSut = GetSut(cache);
        var result = await localSut.RefreshCachedAsset<OrchestrationImage>(assetId, true);
        
        // Assert
        result.Version.Should().Be(11);
        result.AssetId.Should().Be(assetId);
        result.Height.Should().Be(10);
        result.Width.Should().Be(50);
        result.MaxUnauthorised.Should().Be(-1);
        result.OpenThumbs.Should().BeEquivalentTo(sizes);
        result.Status.Should().Be(OrchestrationStatus.Orchestrating);
    }
}

public class DictionaryCache : IAppCache
{
    public Dictionary<string, object> InMemoryList { get; } = new();
    
    public void Add<T>(string key, T item, MemoryCacheEntryOptions policy) => InMemoryList[key] = item;

    public T Get<T>(string key)
    {
        if (InMemoryList.TryGetValue(key, out var cached))
        {
            if (cached is T typedCached)
            {
                return typedCached;
            }
        }

        return default;
    }

    public Task<T> GetAsync<T>(string key) => Task.FromResult(Get<T>(key));

    public bool TryGetValue<T>(string key, out T value)
    {
        if (InMemoryList.TryGetValue(key, out var cached))
        {
            if (cached is T typedCached)
            {
                value = typedCached;
                return true;
            }
        }

        value = default;
        return false;
    }

    public T GetOrAdd<T>(string key, Func<ICacheEntry, T> addItemFactory)
    {
        if (TryGetValue<T>(key, out T value))
        {
            return value;
        }
        
        var result = addItemFactory((ICacheEntry) new MockCacheEntry(key));
        InMemoryList[key] = result;
        return result;
    }

    public T GetOrAdd<T>(string key, Func<ICacheEntry, T> addItemFactory, MemoryCacheEntryOptions policy)
        => GetOrAdd(key, addItemFactory);

    public async Task<T> GetOrAddAsync<T>(string key, Func<ICacheEntry, Task<T>> addItemFactory)
    {
        if (TryGetValue(key, out T value))
        {
            return value;
        }
        
        var result = await addItemFactory((ICacheEntry) new MockCacheEntry(key));
        InMemoryList[key] = result;
        return result;
    }

    public Task<T> GetOrAddAsync<T>(string key, Func<ICacheEntry, Task<T>> addItemFactory, MemoryCacheEntryOptions policy)
        => GetOrAddAsync(key, addItemFactory);

    public void Remove(string key) => InMemoryList.Remove(key);

    public ICacheProvider CacheProvider { get; }
    public CacheDefaults DefaultCachePolicy { get; set; } = new CacheDefaults();
}