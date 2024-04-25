using System.Collections.Generic;
using System.Threading;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Policies;
using IIIF;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orchestrator.Infrastructure;
using Orchestrator.Settings;
using Test.Helpers.Data;

namespace Orchestrator.Tests.Infrastructure;

public class MetadataWithFallbackThumbSizeProviderTests
{
    private const int DeliveryChannelPolicyId = 999;
    private readonly MetadataWithFallbackThumbSizeProvider sut;
    private readonly IPolicyRepository policyRepository;
    public MetadataWithFallbackThumbSizeProviderTests()
    {
        policyRepository = A.Fake<IPolicyRepository>();

        var orchestratorSettings = Options.Create(new OrchestratorSettings());
        sut = new MetadataWithFallbackThumbSizeProvider(policyRepository, orchestratorSettings,
            new NullLogger<MetadataWithFallbackThumbSizeProvider>());
    }

    [Fact]
    public async Task GetAvailableThumbSizesForImage_ReturnsOpenFromMetadata_IfMetadataAttached_AndOpenOnly()
    {
        // Arrange
        const string thumbsMetadata = "{\"a\": [[769, 1024],[300,400]], \"o\": [[150, 200],[75, 100]]}";
        var expected = new List<Size> { new(150, 200), new(75, 100) };
        var assetId = AssetIdGenerator.GetAssetId();
        var asset = new Asset(assetId).WithTestThumbnailMetadata(thumbsMetadata);
        
        // Act
        var actual = await sut.GetThumbSizesForImage(asset, true);
        
        // Assert
        actual.Should().BeEquivalentTo(expected);
    }
    
    [Fact]
    public async Task GetAvailableThumbSizesForImage_ReturnsAllFromMetadata_IfMetadataAttached_AndNotOpenOnly()
    {
        // Arrange
        const string thumbsMetadata = "{\"a\": [[769, 1024],[300,400]], \"o\": [[150, 200],[75, 100]]}";
        var expected = new List<Size> { new(769, 1024), new(300, 400), new(150, 200), new(75, 100) };
        var assetId = AssetIdGenerator.GetAssetId();
        var asset = new Asset(assetId).WithTestThumbnailMetadata(thumbsMetadata);
        
        // Act
        var actual = await sut.GetThumbSizesForImage(asset, false);
        
        // Assert
        actual.Should().BeEquivalentTo(expected);
    }
    
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetAvailableThumbSizesForImage_ReturnsEmptyList_IfMetadataAttached_ButNoThumbs(bool openOnly)
    {
        // Arrange
        const string thumbsMetadata = "{\"a\": [], \"o\": []}";
        var assetId = AssetIdGenerator.GetAssetId();
        var asset = new Asset(assetId).WithTestThumbnailMetadata(thumbsMetadata);
        
        // Act
        var actual = await sut.GetThumbSizesForImage(asset, openOnly);
        
        // Assert
        actual.Should().BeEmpty();
    }
    
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetAvailableThumbSizesForImage_ReturnsEmptyList_IfNoMetadata_AndNoThumbsChannel(bool openOnly)
    {
        // Arrange
        var asset = new Asset(AssetIdGenerator.GetAssetId());
        
        // Act
        var actual = await sut.GetThumbSizesForImage(asset, openOnly);
        
        // Assert
        actual.Should().BeEmpty();
    }
    
    [Fact]
    public async Task GetAvailableThumbSizesForImage_ReturnsCalculatedAvailableSizes_IfNoMetadataAttached_AndOpenOnly()
    {
        // Arrange
        // only confined sizes calculated (so 100, is ignored)
        var expected = new List<Size> { new(200, 400), new(100, 200) }; 
        var assetId = AssetIdGenerator.GetAssetId();
        var asset = GetAssetWithThumbsChannel(assetId, 1000, 2000, 500);
        
        var policy = new DeliveryChannelPolicy
        {
            PolicyData = "[\"!1000,1000\", \"!400,400\", \"!200,200\", \"100,\"]",
            Channel = "thumbs",
            Id = DeliveryChannelPolicyId,
        };
        A.CallTo(() =>
                policyRepository.GetThumbnailPolicy(DeliveryChannelPolicyId, asset.Customer, A<CancellationToken>._))
            .Returns(policy);
            
        // Act
        var actual = await sut.GetThumbSizesForImage(asset, true);
        
        // Assert
        actual.Should().BeEquivalentTo(expected);
    }
    
    [Fact]
    public async Task GetAvailableThumbSizesForImage_ReturnsAllCalculatedSizes_IfNoMetadataAttached_AndNotOpenOnly()
    {
        // Arrange
        // only confined sizes calculated (so 100, is ignored)
        var expected = new List<Size> { new(500, 1000), new(200, 400), new(100, 200) }; 
        var assetId = AssetIdGenerator.GetAssetId();
        var asset = GetAssetWithThumbsChannel(assetId, 1000, 2000, 250);
        
        var policy = new DeliveryChannelPolicy
        {
            PolicyData = "[\"!1000,1000\", \"!400,400\", \"!200,200\", \"100,\"]",
            Channel = "thumbs",
            Id = DeliveryChannelPolicyId,
        };
        A.CallTo(() =>
                policyRepository.GetThumbnailPolicy(DeliveryChannelPolicyId, asset.Customer, A<CancellationToken>._))
            .Returns(policy);
            
        // Act
        var actual = await sut.GetThumbSizesForImage(asset, false);
        
        // Assert
        actual.Should().BeEquivalentTo(expected);
    }

    private Asset GetAssetWithThumbsChannel(AssetId assetId, int w, int h, int maxUnauth)
    {
        var asset = new Asset(assetId)
        {
            Width = w,
            Height = h,
            MaxUnauthorised = maxUnauth,
            ImageDeliveryChannels = new List<ImageDeliveryChannel>
            {
                new()
                {
                    Channel = AssetDeliveryChannels.Thumbnails,
                    DeliveryChannelPolicyId = DeliveryChannelPolicyId
                }
            }
        };
        return asset;
    }
}
