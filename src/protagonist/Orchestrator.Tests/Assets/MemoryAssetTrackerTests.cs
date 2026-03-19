using System;
using System.Collections.Generic;
using DLCS.Core.Caching;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Customers;
using LazyCache.Mocks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orchestrator.Assets;
using Orchestrator.Infrastructure.DataAccess;
using Orchestrator.Settings;
using Test.Helpers.Data;

namespace Orchestrator.Tests.Assets;

public class MemoryAssetTrackerTests
{
    private readonly IOrchestratorAssetRepository assetRepository;
    private readonly IOrchestratorAdjunctRepository adjunctRepository;
    private readonly IThumbRepository thumbRepository;
    private readonly ICustomerOriginStrategyRepository customerOriginStrategyRepository;
    private readonly MemoryAssetTracker sut;

    public MemoryAssetTrackerTests()
    {
        assetRepository = A.Fake<IOrchestratorAssetRepository>();
        adjunctRepository = A.Fake<IOrchestratorAdjunctRepository>();
        thumbRepository = A.Fake<IThumbRepository>();
        customerOriginStrategyRepository = A.Fake<ICustomerOriginStrategyRepository>();
        A.CallTo(() => customerOriginStrategyRepository.GetCustomerOriginStrategy(A<AssetId>._, A<string>._))
            .Returns(Task.FromResult(new CustomerOriginStrategy { Id = "_default_", Strategy = OriginStrategyType.Default }));

        sut = GetSut();
    }

    private MemoryAssetTracker GetSut(DateTime? emptyImageLocationCreatedDate = null, int maxWidth = 5000)
    {
        var orchestratorSettings = new OrchestratorSettings
        {
            MaxWidth = maxWidth,
            Caching = new CacheSettings(),
            ReingestOnOrchestration = new ReingestOnOrchestrationSettings
            {
                EmptyImageLocationCreatedDate = emptyImageLocationCreatedDate
            }
        };
        return new MemoryAssetTracker(assetRepository, adjunctRepository, new MockCachingService(), thumbRepository,
            customerOriginStrategyRepository, Options.Create(orchestratorSettings),
            new NullLogger<MemoryAssetTracker>());
    }

    [Fact]
    public async Task GetOrchestrationAsset_Null_IfNotFound()
    {
        // Arrange
        var assetId = new AssetId(1, 1, "go!");
        A.CallTo(() => assetRepository.GetAsset(assetId, false)).Returns<Asset>(null);
        
        // Act
        var result = await sut.GetOrchestrationAsset(assetId);
        
        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData("iiif-img", typeof(OrchestrationImage), AvailableDeliveryChannel.Image)]
    [InlineData("iiif-av", typeof(OrchestrationAsset), AvailableDeliveryChannel.Timebased)]
    [InlineData("file", typeof(OrchestrationAsset), AvailableDeliveryChannel.File)]
    [InlineData("iiif-img,file", typeof(OrchestrationImage), AvailableDeliveryChannel.Image | AvailableDeliveryChannel.File)]
    [InlineData("iiif-av,file", typeof(OrchestrationAsset), AvailableDeliveryChannel.Timebased | AvailableDeliveryChannel.File)]
    public async Task GetOrchestrationAsset_ReturnsCorrectType(string deliveryChannels, Type expectedType,
        AvailableDeliveryChannel channel)
    {
        // Arrange
        var imageDeliveryChannels = deliveryChannels.GenerateDeliveryChannels();
        var assetId = new AssetId(1, 1, "go!");
        A.CallTo(() => assetRepository.GetAsset(assetId, false))
            .Returns(new Asset { ImageDeliveryChannels = imageDeliveryChannels, Origin = "test" });

        // Act
        var result = await sut.GetOrchestrationAsset(assetId);

        // Assert
        result!.AssetId.Should().Be(assetId);
        result.Channels.Should().Be(channel);
        result.Should().BeOfType(expectedType);
    }

    [Theory]
    [InlineData("iiif-img")]
    [InlineData("iiif-av")]
    [InlineData("file")]
    [InlineData("iiif-img,file")]
    [InlineData("iiif-av,file")]
    public async Task GetOrchestrationAsset_Null_IfAssetFoundButNotForDelivery(string deliveryChannels)
    {
        // Arrange
        var imageDeliveryChannels = deliveryChannels.GenerateDeliveryChannels();
        var assetId = new AssetId(1, 1, "go!");
        A.CallTo(() => assetRepository.GetAsset(assetId, false))
            .Returns(new Asset { ImageDeliveryChannels = imageDeliveryChannels, NotForDelivery = true });
        
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
        A.CallTo(() => assetRepository.GetAsset(assetId, false)).Returns<Asset>(null);
        
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
        A.CallTo(() => assetRepository.GetAsset(assetId, false)).Returns<Asset>(null);
        
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
        A.CallTo(() => assetRepository.GetAsset(assetId, false)).Returns(new Asset { NotForDelivery = true });
        
        // Act
        var result = await sut.GetOrchestrationAsset<OrchestrationImage>(assetId);
        
        // Assert
        result.Should().BeNull();
    }
    
    [Theory]
    [InlineData("iiif-img", null)]
    [InlineData("iiif-img,file", "my-origin")]
    public async Task GetOrchestrationAssetT_ReturnsOrchestrationAsset_IfImage(string deliveryChannels, string expectedOrigin)
    {
        // Arrange
        var imageDeliveryChannels = deliveryChannels.GenerateDeliveryChannels();
        var assetId = new AssetId(1, 1, "go!");
        A.CallTo(() => assetRepository.GetAsset(assetId, false))
            .Returns(new Asset
            {
                ImageDeliveryChannels = imageDeliveryChannels, Origin = "my-origin"
            });
        
        // Act
        var result = await sut.GetOrchestrationAsset<OrchestrationAsset>(assetId);
        
        // Assert
        result!.AssetId.Should().Be(assetId);
        result.Origin.Should().Be(expectedOrigin);
        A.CallTo(() => thumbRepository.GetOpenSizes(A<AssetId>._)).MustHaveHappened();
    }
    
    [Theory]
    [InlineData("iiif-av", null)]
    [InlineData("file", "my-origin")]
    [InlineData("iiif-av,file", "my-origin")]
    public async Task GetOrchestrationAssetT_ReturnsOrchestrationAsset(string deliveryChannels, string expectedOrigin)
    {
        // Arrange
        var imageDeliveryChannels = deliveryChannels.GenerateDeliveryChannels();
        var assetId = new AssetId(1, 1, "go!");
        A.CallTo(() => assetRepository.GetAsset(assetId, false))
            .Returns(new Asset
            {
                ImageDeliveryChannels = imageDeliveryChannels, Origin = "my-origin"
            });
        
        // Act
        var result = await sut.GetOrchestrationAsset<OrchestrationAsset>(assetId);
        
        // Assert
        result!.AssetId.Should().Be(assetId);
        result.Origin.Should().Be(expectedOrigin);
        A.CallTo(() => thumbRepository.GetOpenSizes(A<AssetId>._)).MustNotHaveHappened();
    }
    
    [Theory]
    [InlineData("iiif-img")]
    [InlineData("iiif-img,file")]
    public async Task GetOrchestrationAssetT_ReturnsOrchestrationImage(string deliveryChannels)
    {
        // Arrange
        var imageDeliveryChannels = deliveryChannels.GenerateDeliveryChannels();

        var assetId = new AssetId(1, 1, "go!");

        var sizes = new List<int[]> { new[] { 100, 200 } };
        A.CallTo(() => assetRepository.GetAsset(assetId, false)).Returns(new Asset
        {
            ImageDeliveryChannels = imageDeliveryChannels,
            Height = 10, Width = 50, Origin = "test"
        });
        A.CallTo(() => thumbRepository.GetOpenSizes(assetId)).Returns(sizes);

        // Act
        var result = await sut.GetOrchestrationAsset<OrchestrationImage>(assetId);
        
        // Assert
        result!.AssetId.Should().Be(assetId);
        result.Size.Height.Should().Be(10);
        result.Size.Width.Should().Be(50);
        result.OpenThumbs.Should().BeEquivalentTo(sizes);
        result.Reingest.Should().BeFalse();
    }
    
    [Theory]
    [InlineData("", 0, null)]
    [InlineData("", 100, null)]
    [InlineData("role", 0, 0)]
    [InlineData("role", 100, 100)]
    public async Task GetOrchestrationAsset_SetsOpenFullMax_IfHasRole(string roles, int openFullMax, int? expected)
    {
        // Arrange
        var imageDeliveryChannels = "iiif-img".GenerateDeliveryChannels();
        var assetId = new AssetId(1, 1, "go!");
        A.CallTo(() => assetRepository.GetAsset(assetId, false)).Returns(new Asset
        {
            ImageDeliveryChannels = imageDeliveryChannels, Roles = roles, OpenFullMax = openFullMax
        });
        
        // Act
        var result = await sut.GetOrchestrationAsset<OrchestrationImage>(assetId);
        
        // Assert
        result!.OpenFullMax.Should().Be(expected);
    }
    
    [Theory]
    [InlineData(250, 250, 250)]
    [InlineData(0, 100, 100)]
    [InlineData(5000, 250, 250)]
    [InlineData(250, 5000, 250)]
    public async Task GetOrchestrationAsset_SetsMaxWidth(int assetMaxWidth, int systemMaxWidth, int expected)
    {
        // Arrange
        var imageDeliveryChannels = "iiif-img".GenerateDeliveryChannels();
        var assetId = new AssetId(1, 1, "go!");
        A.CallTo(() => assetRepository.GetAsset(assetId, false)).Returns(new Asset
        {
            ImageDeliveryChannels = imageDeliveryChannels, MaxWidth = assetMaxWidth
        });
        
        // Act
        var localSut = GetSut(maxWidth: systemMaxWidth);
        var result = await localSut.GetOrchestrationAsset<OrchestrationImage>(assetId);
        
        // Assert
        result!.MaxWidth.Should().Be(expected);
    }
    
    [Theory]
    [InlineData("iiif-img")]
    [InlineData("iiif-img,file")]
    public async Task GetOrchestrationAssetT_SetsOpenThumbsToEmpty_IfNullReturned(string deliveryChannels)
    {
        // Arrange
        var imageDeliveryChannels = deliveryChannels.GenerateDeliveryChannels();
        var assetId = new AssetId(1, 1, "otis");
        A.CallTo(() => assetRepository.GetAsset(assetId, false)).Returns(new Asset
        {
            ImageDeliveryChannels = imageDeliveryChannels, Height = 10, Width = 50,
            Origin = "test", Created = DateTime.Today
        });
        A.CallTo(() => thumbRepository.GetOpenSizes(assetId)).Returns<List<int[]>>(null);
        
        // Act
        var result = await sut.GetOrchestrationAsset<OrchestrationImage>(assetId);

        // Assert
        result!.OpenThumbs.Should().BeEmpty();
    }

    [Theory]
    [InlineData("iiif-img")]
    [InlineData("iiif-img,file")]
    public async Task GetOrchestrationAssetT_Reingest_True_IfCreatedBeforeCutOff(string deliveryChannels)
    {
        // Arrange
        var imageDeliveryChannels = deliveryChannels.GenerateDeliveryChannels();
        var assetId = new AssetId(1, 1, "otis");
        A.CallTo(() => assetRepository.GetAsset(assetId, false)).Returns(new Asset
        {
            ImageDeliveryChannels = imageDeliveryChannels, Height = 10, Width = 50,
            Origin = "test", Created = DateTime.Today.AddDays(-1)
        });
        A.CallTo(() => thumbRepository.GetOpenSizes(assetId)).Returns<List<int[]>>(null);

        // Act
        var localSut = GetSut(DateTime.Today);
        var result = await localSut.GetOrchestrationAsset<OrchestrationImage>(assetId);
        
        // Assert
        result!.Reingest.Should().BeTrue();
    }
    
    [Theory]
    [InlineData("iiif-img")]
    [InlineData("iiif-img,file")]
    public async Task GetOrchestrationAssetT_Reingest_False_IfCreatedAfterCutOff(string deliveryChannels)
    {
        // Arrange
        var imageDeliveryChannels = deliveryChannels.GenerateDeliveryChannels();
        var assetId = new AssetId(1, 1, "otis");
        A.CallTo(() => assetRepository.GetAsset(assetId, false)).Returns(new Asset
        {
            ImageDeliveryChannels = imageDeliveryChannels, Height = 10, Width = 50,
            Origin = "test", Created = DateTime.Today.AddDays(1)
        });
        A.CallTo(() => thumbRepository.GetOpenSizes(assetId)).Returns<List<int[]>>(null);

        // Act
        var localSut = GetSut(DateTime.Today);
        var result = await localSut.GetOrchestrationAsset<OrchestrationImage>(assetId);
        
        // Assert
        result!.Reingest.Should().BeFalse();
    }
    
    [Theory]
    [InlineData("iiif-av")]
    [InlineData("file")]
    [InlineData("iiif-av,file")]
    public async Task GetOrchestrationAssetT_Null_IfWrongTypeAskedFor(string deliveryChannels)
    {
        // Arrange
        var imageDeliveryChannels = deliveryChannels.GenerateDeliveryChannels();
        var assetId = new AssetId(1, 1, "go!");
        A.CallTo(() => assetRepository.GetAsset(assetId, false))
            .Returns(new Asset { ImageDeliveryChannels = imageDeliveryChannels, Origin = "test" });
        
        // Act
        var result = await sut.GetOrchestrationAsset<OrchestrationImage>(assetId);
        
        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("role", true)]
    public async Task GetOrchestrationAsset_SetsRequiresAuth_BaseOnRoles(string roles, bool requiresAuth)
    {
        // Arrange
        var imageDeliveryChannels = "iiif-img".GenerateDeliveryChannels();
        var assetId = new AssetId(1, 1, "go!");
        A.CallTo(() => assetRepository.GetAsset(assetId, false)).Returns(new Asset
        {
            ImageDeliveryChannels = imageDeliveryChannels, Roles = roles
        });
        
        // Act
        var result = await sut.GetOrchestrationAsset<OrchestrationImage>(assetId);
        
        // Assert
        result!.RequiresAuth.Should().Be(requiresAuth);
    }
    
    [Theory]
    [InlineData("file")]
    [InlineData("iiif-av,file")]
    [InlineData("iiif-img,file")]
    public async Task GetOrchestrationAssetT_Throws_IfFileDeliveryChannel_AndNoOrigin(string deliveryChannels)
    {
        // Arrange
        var imageDeliveryChannels = deliveryChannels.GenerateDeliveryChannels();
        var assetId = new AssetId(1, 1, "go!");

        A.CallTo(() => assetRepository.GetAsset(assetId, false))
            .Returns(new Asset { ImageDeliveryChannels = imageDeliveryChannels});
        
        // Act
        Func<Task> action = () => sut.GetOrchestrationAsset<OrchestrationAsset>(assetId);
        
        // Assert
        await action.Should().ThrowAsync<ArgumentNullException>();
    }
    
    [Theory]
    [InlineData("file", true)]
    [InlineData("file", false)]
    [InlineData("iiif-av,file", true)]
    [InlineData("iiif-av,file", false)]
    [InlineData("iiif-img,file", true)]
    [InlineData("iiif-img,file", false)]
    public async Task GetOrchestrationAssetT_SetsOptimisedAndMediaType_IfFileDeliveryChannel(string deliveryChannels, bool optimised)
    {
        // Arrange
        var imageDeliveryChannels = deliveryChannels.GenerateDeliveryChannels();
        var assetId = new AssetId(1, 1, "go!");
        A.CallTo(() => customerOriginStrategyRepository.GetCustomerOriginStrategy(assetId, A<string>._))
            .Returns(Task.FromResult(new CustomerOriginStrategy
                { Id = "_default_", Strategy = OriginStrategyType.Default, Optimised = optimised }));
        
        A.CallTo(() => assetRepository.GetAsset(assetId, false))
            .Returns(new Asset
            {
                ImageDeliveryChannels = imageDeliveryChannels, Origin = "test", MediaType = "audio/mpeg"
            });
        
        // Act
        var result = await sut.GetOrchestrationAsset<OrchestrationAsset>(assetId);
        
        // Assert
        result!.OptimisedOrigin.Should().Be(optimised);
        result.MediaType.ToString().Should().Be("audio/mpeg");
    }
    
    [Fact]
    public async Task RefreshCachedAsset_Null_IfNotFound()
    {
        // Arrange
        var assetId = new AssetId(1, 1, "go!");
        A.CallTo(() => assetRepository.GetAsset(assetId, true)).Returns<Asset>(null);
        
        // Act
        var result = await sut.RefreshCachedAsset<OrchestrationAsset>(assetId);
        
        // Assert
        result.Should().BeNull();
    }
    
    [Fact]
    public async Task RefreshCachedAsset_Null_IfAssetFoundButNotForDelivery()
    {
        // Arrange
        var assetId = new AssetId(1, 1, "go!");
        A.CallTo(() => assetRepository.GetAsset(assetId, true)).Returns(new Asset { NotForDelivery = true });
        
        // Act
        var result = await sut.GetOrchestrationAsset<OrchestrationImage>(assetId);
        
        // Assert
        result.Should().BeNull();
    }
    
    [Fact]
    public async Task GetOrchestrationAdjunct_Null_IfNotFound()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId(1);
        const string adjunctId = "nope";
        A.CallTo(() => adjunctRepository.GetAdjunct(adjunctId, assetId, false)).Returns<Adjunct>(null);
        
        // Act
        var result = await sut.GetOrchestrationAdjunct(adjunctId, assetId);
        
        // Assert
        result.Should().BeNull("repo mock set to return null");
    }

    [Fact]
    public async Task GetOrchestrationAdjunct_ReturnsOrchestrationAdjunct()
    {
        // Arrange
        var assetId = new AssetId(1, 1, nameof(GetOrchestrationAdjunct_ReturnsOrchestrationAdjunct));
        const string adjunctId = "yup";
        const string origin = "http://example.com/some-origin";
        A.CallTo(() => adjunctRepository.GetAdjunct(adjunctId, assetId, false)).Returns(
            new Adjunct
            {
                Id = adjunctId,
                AssetId = assetId,
                Origin = origin,
                MediaType = "application/json",
                IIIFLink = IIIFLinkType.SeeAlso,
                Type = "a_type"
            });

        A.CallTo(() => customerOriginStrategyRepository.GetCustomerOriginStrategy(assetId, origin))
            .Returns(new CustomerOriginStrategy { Id = "_default_", 
                Optimised = true, Strategy = OriginStrategyType.Default });
        
        // Act
        var result = await sut.GetOrchestrationAdjunct(adjunctId, assetId);
        
        // Assert
        result.Should().NotBeNull("mock repo set to return a valid obj");
        result!.Id.Should().Be(adjunctId, "as per repo setting");
        result.AssetId.Should().Be(assetId, "as per repo setting");
        result.Origin.Should().Be(origin, "as per repo setting");
        result.MediaType.ToString().Should().Be("application/json", "as per repo setting");
        result.OptimisedOrigin.Should().Be(true, "as per cos repo setting");
    }

    [Fact]
    public async Task RefreshCachedAdjunct_Null_IfNotFound()
    {
        // Arrange
        var assetId = new AssetId(1, 1, nameof(RefreshCachedAdjunct_Null_IfNotFound));
        const string adjunctId = "nope";
        A.CallTo(() => adjunctRepository.GetAdjunct(adjunctId, assetId, true)).Returns<Adjunct>(null);

        // Act
        var result = await sut.RefreshCachedAdjunct(adjunctId, assetId);
        
        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task RefreshCachedAdjunct_ReturnsAdjunct()
    {
        var assetId = new AssetId(1, 1, nameof(RefreshCachedAdjunct_ReturnsAdjunct));
        const string adjunctId = "yup";
        const string origin = "http://example.com/some-origin";
        A.CallTo(() => adjunctRepository.GetAdjunct(adjunctId, assetId, true)).Returns(
            new Adjunct
            {
                Id = adjunctId,
                AssetId = assetId,
                Origin = origin,
                MediaType = "application/json",
                IIIFLink = IIIFLinkType.SeeAlso,
                Type = "a_type"
            });

        A.CallTo(() => customerOriginStrategyRepository.GetCustomerOriginStrategy(assetId, origin))
            .Returns(new CustomerOriginStrategy { Id = "_default_", 
                Optimised = true, Strategy = OriginStrategyType.Default });
        
        // Act
        var result = await sut.RefreshCachedAdjunct(adjunctId, assetId);
        
        // Assert
        result.Should().NotBeNull("mock repo set to return a valid obj");
        result!.Id.Should().Be(adjunctId, "as per repo setting");
        result.AssetId.Should().Be(assetId, "as per repo setting");
        result.Origin.Should().Be(origin, "as per repo setting");
        result.MediaType.ToString().Should().Be("application/json", "as per repo setting");
        result.OptimisedOrigin.Should().Be(true, "as per cos repo setting");
    }
}
