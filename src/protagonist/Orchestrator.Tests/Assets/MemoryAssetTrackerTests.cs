using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DLCS.Core.Caching;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using FakeItEasy;
using FluentAssertions;
using LazyCache.Mocks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orchestrator.Assets;
using Xunit;

namespace Orchestrator.Tests.Assets;

public class MemoryAssetTrackerTests
{
    private readonly IAssetRepository assetRepository;
    private readonly IThumbRepository thumbRepository;
    private readonly MemoryAssetTracker sut;

    public MemoryAssetTrackerTests()
    {
        assetRepository = A.Fake<IAssetRepository>();
        thumbRepository = A.Fake<IThumbRepository>();

        sut = new MemoryAssetTracker(assetRepository, new MockCachingService(), thumbRepository,
            Options.Create(new CacheSettings()), new NullLogger<MemoryAssetTracker>());
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
    [InlineData("iiif-img", typeof(OrchestrationImage), AvailableDeliveryChannel.Image)]
    [InlineData("iiif-av", typeof(OrchestrationAsset), AvailableDeliveryChannel.Timebased)]
    [InlineData("file", typeof(OrchestrationAsset), AvailableDeliveryChannel.File)]
    [InlineData("iiif-img,file", typeof(OrchestrationImage), AvailableDeliveryChannel.Image | AvailableDeliveryChannel.File)]
    [InlineData("iiif-av,file", typeof(OrchestrationAsset), AvailableDeliveryChannel.Timebased | AvailableDeliveryChannel.File)]
    public async Task GetOrchestrationAsset_ReturnsCorrectType(string deliveryChannel, Type expectedType,
        AvailableDeliveryChannel channel)
    {
        // Arrange
        var assetId = new AssetId(1, 1, "go!");
        A.CallTo(() => assetRepository.GetAsset(assetId))
            .Returns(new Asset { DeliveryChannel = deliveryChannel.Split(",") });

        // Act
        var result = await sut.GetOrchestrationAsset(assetId);

        // Assert
        result.AssetId.Should().Be(assetId);
        result.Channels.Should().Be(channel);
        result.Should().BeOfType(expectedType);
    }

    [Theory]
    [InlineData("iiif-img")]
    [InlineData("iiif-av")]
    [InlineData("file")]
    [InlineData("iiif-img,file")]
    [InlineData("iiif-av,file")]
    public async Task GetOrchestrationAsset_Null_IfAssetFoundButNotForDelivery(string deliveryChannel)
    {
        // Arrange
        var assetId = new AssetId(1, 1, "go!");
        A.CallTo(() => assetRepository.GetAsset(assetId))
            .Returns(new Asset { DeliveryChannel = deliveryChannel.Split(","), NotForDelivery = true });
        
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
    [InlineData("iiif-av", null)]
    [InlineData("file", "my-origin")]
    [InlineData("iiif-av,file", "my-origin")]
    public async Task GetOrchestrationAssetT_ReturnsOrchestrationAsset(string deliveryChannel, string expectedOrigin)
    {
        // Arrange
        var assetId = new AssetId(1, 1, "go!");
        A.CallTo(() => assetRepository.GetAsset(assetId))
            .Returns(new Asset
            {
                DeliveryChannel = deliveryChannel.Split(","), Origin = "my-origin"
            });
        
        // Act
        var result = await sut.GetOrchestrationAsset<OrchestrationAsset>(assetId);
        
        // Assert
        result.AssetId.Should().Be(assetId);
        result.Origin.Should().Be(expectedOrigin);
        A.CallTo(() => thumbRepository.GetOpenSizes(A<AssetId>._)).MustNotHaveHappened();
    }
    
    [Theory]
    [InlineData("iiif-img")]
    [InlineData("iiif-img,file")]
    public async Task GetOrchestrationAssetT_ReturnsOrchestrationImage(string deliveryChannel)
    {
        // Arrange
        var assetId = new AssetId(1, 1, "go!");
        var sizes = new List<int[]> { new[] { 100, 200 } };
        A.CallTo(() => assetRepository.GetAsset(assetId)).Returns(new Asset
        {
            DeliveryChannel = deliveryChannel.Split(","), Height = 10, Width = 50, MaxUnauthorised = -1
        });
        A.CallTo(() => thumbRepository.GetOpenSizes(assetId)).Returns(sizes);

        // Act
        var result = await sut.GetOrchestrationAsset<OrchestrationImage>(assetId);
        
        // Assert
        result.AssetId.Should().Be(assetId);
        result.Height.Should().Be(10);
        result.Width.Should().Be(50);
        result.MaxUnauthorised.Should().Be(-1);
        result.OpenThumbs.Should().BeEquivalentTo(sizes);
    }
    
    [Theory]
    [InlineData("iiif-av")]
    [InlineData("file")]
    [InlineData("iiif-av,file")]
    public async Task GetOrchestrationAssetT_Null_IfWrongTypeAskedFor(string deliveryChannel)
    {
        // Arrange
        var assetId = new AssetId(1, 1, "go!");
        A.CallTo(() => assetRepository.GetAsset(assetId))
            .Returns(new Asset { DeliveryChannel = deliveryChannel.Split(",") });
        
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
            DeliveryChannel = new[] { "iiif-img" }, MaxUnauthorised = maxUnauth, Roles = roles
        });
        
        // Act
        var result = await sut.GetOrchestrationAsset<OrchestrationImage>(assetId);
        
        // Assert
        result.RequiresAuth.Should().Be(requiresAuth);
    }
}