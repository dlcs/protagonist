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
    public async Task GetAvailableThumbSizesForImage_ReturnsFromMetadata_IfMetadataAttached()
    {
        // Arrange
        const string thumbsMetadata = "{\"a\": [[769, 1024],[300,400]], \"o\": [[150, 200],[75, 100]]}";
        var expected = new ThumbnailSizes(
            new List<int[]> { new[] { 150, 200 }, new[] { 75, 100 }, },
            new List<int[]> { new[] { 769, 1024 }, new[] { 300, 400 }, });
        
        var assetId = AssetIdGenerator.GetAssetId();
        var asset = new Asset(assetId).WithTestThumbnailMetadata(thumbsMetadata);
        
        // Act
        var actual = await sut.GetThumbSizesForImage(asset);
        
        // Assert
        actual.Should().BeEquivalentTo(expected);
    }
    
    [Fact]
    public async Task GetAvailableThumbSizesForImage_ReturnsEmptyList_IfMetadataAttached_ButNoThumbs()
    {
        // Arrange
        const string thumbsMetadata = "{\"a\": [], \"o\": []}";
        var assetId = AssetIdGenerator.GetAssetId();
        var asset = new Asset(assetId).WithTestThumbnailMetadata(thumbsMetadata);
        
        // Act
        var actual = await sut.GetThumbSizesForImage(asset);
        
        // Assert
        actual.Should().BeEquivalentTo(ThumbnailSizes.Empty);
    }
    
    [Fact]
    public async Task GetAvailableThumbSizesForImage_ReturnsEmptyList_IfNoMetadata_AndNoThumbsChannel()
    {
        // Arrange
        var asset = new Asset(AssetIdGenerator.GetAssetId());
        
        // Act
        var actual = await sut.GetThumbSizesForImage(asset);
        
        // Assert
        actual.Should().BeEquivalentTo(ThumbnailSizes.Empty);
    }
    
    [Fact]
    public async Task GetAvailableThumbSizesForImage_ReturnsAllCalculatedSizes_IfNoMetadataAttached()
    {
        // Arrange
        var expected = new ThumbnailSizes(
            new List<int[]> { new[] { 100, 200 }, new[] { 75, 150 }, },
            new List<int[]> { new[] { 500, 1000 }, new[] { 200, 400 }, });
        
        var assetId = AssetIdGenerator.GetAssetId();
        var asset = GetAssetWithThumbsChannel(assetId, 1000, 2000, 250);
        
        var policy = new DeliveryChannelPolicy
        {
            PolicyData = "[\"!1000,1000\", \"!400,400\", \"!200,200\", \"75,\"]",
            Channel = "thumbs",
            Id = DeliveryChannelPolicyId,
        };
        A.CallTo(() =>
                policyRepository.GetThumbnailPolicy(DeliveryChannelPolicyId, asset.Customer, A<CancellationToken>._))
            .Returns(policy);
            
        // Act
        var actual = await sut.GetThumbSizesForImage(asset);
        
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
