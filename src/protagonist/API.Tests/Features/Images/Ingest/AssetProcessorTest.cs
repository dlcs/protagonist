using System;
using System.Threading;
using System.Threading.Tasks;
using API.Features.Assets;
using API.Features.Image;
using API.Features.Image.Ingest;
using API.Settings;
using DLCS.Core.Types;
using DLCS.HydraModel;
using DLCS.Model.Assets;
using DLCS.Model.DeliveryChannels;
using DLCS.Model.Storage;
using FakeItEasy;
using Test.Helpers.Settings;
using CustomerStorage = DLCS.Model.Storage.CustomerStorage;
using StoragePolicy = DLCS.Model.Storage.StoragePolicy;

namespace API.Tests.Features.Images.Ingest;

public class AssetProcessorTest
{
    private readonly AssetProcessor sut;
    private readonly IApiAssetRepository assetRepository;
    private readonly IStorageRepository storageRepository;
    private readonly IDeliveryChannelPolicyRepository deliveryChannelPolicyRepository;
    private readonly IDefaultDeliveryChannelRepository defaultDeliveryChannelRepository;

    public AssetProcessorTest()
    {
        var apiSettings = new ApiSettings();
        storageRepository = A.Fake<IStorageRepository>();
        assetRepository = A.Fake<IApiAssetRepository>();
        defaultDeliveryChannelRepository = A.Fake<IDefaultDeliveryChannelRepository>();
        deliveryChannelPolicyRepository = A.Fake<IDeliveryChannelPolicyRepository>();
        
        var optionsMonitor = OptionsHelpers.GetOptionsMonitor(apiSettings);

        sut = new AssetProcessor(assetRepository, storageRepository, defaultDeliveryChannelRepository, deliveryChannelPolicyRepository, optionsMonitor);
    }
    
    [Fact]
    public async Task Process_ChecksForMaximumNumberOfImages_Exceeded()
    {
        // Arrange
        A.CallTo(() => assetRepository.GetAsset(A<AssetId>._, A<bool>._, A<bool>._)).Returns<Asset?>(null);

        A.CallTo(() => storageRepository.GetStorageMetrics(A<int>._, A<CancellationToken>._))
            .Returns(new AssetStorageMetric
            {
                Policy = new StoragePolicy{MaximumTotalSizeOfStoredImages = 1000, MaximumNumberOfStoredImages = 0},
                CustomerStorage = new CustomerStorage{ TotalSizeOfStoredImages = 10}
            });
        
        var assetBeforeProcessing = new AssetBeforeProcessing(new Asset(), null);
        
        // Act
        var result = await sut.Process(assetBeforeProcessing, false, false, false);
        
        // Assert
        result.Result.IsSuccess.Should().BeFalse();
        result.Result.Error.Should().Be("This operation will fall outside of your storage policy for number of images: maximum is 0");
    }
    
    [Fact]
    public async Task Process_ChecksForTotalImageSize_Exceeded()
    {
        // Arrange
        A.CallTo(() => assetRepository.GetAsset(A<AssetId>._, A<bool>._, A<bool>._)).Returns<Asset?>(null);
        A.CallTo(() => storageRepository.GetStorageMetrics(A<int>._, A<CancellationToken>._))
            .Returns(new AssetStorageMetric
            {
                Policy = new StoragePolicy{MaximumTotalSizeOfStoredImages = -1, MaximumNumberOfStoredImages = 10},
                CustomerStorage = new CustomerStorage{ TotalSizeOfStoredImages = 10}
            });

        var assetBeforeProcessing = new AssetBeforeProcessing(new Asset(), null);
        
        // Act
        var result = await sut.Process(assetBeforeProcessing, false, false, false);
        
        // Assert
        result.Result.IsSuccess.Should().BeFalse();
        result.Result.Error.Should().Be("The total size of stored images has exceeded your allowance: maximum is -1");
    }
    
    [Fact]
    public async Task Process_RetrievesNoneDeliveryChannelPolicy_WhenCalledWithNoneDeliveryChannel()
    {
        // Arrange
        A.CallTo(() => assetRepository.GetAsset(A<AssetId>._, A<bool>._, A<bool>._)).Returns<Asset?>(null);

        A.CallTo(() => storageRepository.GetStorageMetrics(A<int>._, A<CancellationToken>._))
            .Returns(new AssetStorageMetric
            {
                Policy = new StoragePolicy{MaximumTotalSizeOfStoredImages = 1000, MaximumNumberOfStoredImages = 10000},
                CustomerStorage = new CustomerStorage{ TotalSizeOfStoredImages = 10}
            });

        var assetBeforeProcessing = new AssetBeforeProcessing(new Asset()
            {
                Id = new AssetId(1, 1, "asset"),
                MediaType = "image/jpg",
                Origin = "https://some/origin"
            },
            new[]
            {
                new DeliveryChannelsBeforeProcessing("none", null)
            });
        
        // Act
        var result = await sut.Process(assetBeforeProcessing, false, false, false);
        
        // Assert
        result.Result.IsSuccess.Should().BeTrue();
        
        A.CallTo(() =>
                deliveryChannelPolicyRepository.RetrieveDeliveryChannelPolicy(
                    A<int>._, A<string>.That.Matches(x => x == "none"), A<string>.That.Matches(x => x == "none")))
            .MustHaveHappened();
    }
    
    [Fact]
    public async Task Process_RetrievesDeliveryChannelPolicy_WhenCalledWithDeliveryChannels()
    {
        // Arrange
        A.CallTo(() => assetRepository.GetAsset(A<AssetId>._, A<bool>._, A<bool>._)).Returns<Asset?>(null);

        A.CallTo(() => storageRepository.GetStorageMetrics(A<int>._, A<CancellationToken>._))
            .Returns(new AssetStorageMetric
            {
                Policy = new StoragePolicy{MaximumTotalSizeOfStoredImages = 1000, MaximumNumberOfStoredImages = 10000},
                CustomerStorage = new CustomerStorage{ TotalSizeOfStoredImages = 10}
            });

        var assetBeforeProcessing = new AssetBeforeProcessing(new Asset()
        {
            Id = new AssetId(1, 1, "asset"),
            MediaType = "image/jpg",
            Origin = "https://some/origin"
        }, 
            new [] {
            new DeliveryChannelsBeforeProcessing("iiif-img", "somePolicy"),
            new DeliveryChannelsBeforeProcessing("thumbs", "somePolicy")
            });

        // Act
        var result = await sut.Process(assetBeforeProcessing, false, false, false);
        
        // Assert
        result.Result.IsSuccess.Should().BeTrue();
        
        A.CallTo(() =>
                deliveryChannelPolicyRepository.RetrieveDeliveryChannelPolicy(
                    A<int>._, A<string>.That.Matches(x => x == "iiif-img"), A<string>.That.Matches(x => x == "somePolicy")))
            .MustHaveHappened();
        A.CallTo(() =>
                deliveryChannelPolicyRepository.RetrieveDeliveryChannelPolicy(
                    A<int>._, A<string>.That.Matches(x => x == "thumbs"), A<string>.That.Matches(x => x == "somePolicy")))
            .MustHaveHappened();
        A.CallTo(() =>
                deliveryChannelPolicyRepository.RetrieveDeliveryChannelPolicy(
                    A<int>._, A<string>.That.Matches(x => x == "none"), A<string>.That.Matches(x => x == "none")))
            .MustNotHaveHappened();
    }
    
    [Fact]
    public async Task Process_FailsToProcessImage_WhenDeliveryPolicyNotMatched()
    {
        // Arrange
        A.CallTo(() => assetRepository.GetAsset(A<AssetId>._, A<bool>._, A<bool>._)).Returns<Asset?>(null);

        A.CallTo(() => storageRepository.GetStorageMetrics(A<int>._, A<CancellationToken>._))
            .Returns(new AssetStorageMetric
            {
                Policy = new StoragePolicy{MaximumTotalSizeOfStoredImages = 1000, MaximumNumberOfStoredImages = 10000},
                CustomerStorage = new CustomerStorage{ TotalSizeOfStoredImages = 10}
            });
        
        A.CallTo(() => deliveryChannelPolicyRepository.RetrieveDeliveryChannelPolicy(A<int>._, A<string>._, A<string>._))
            .Throws<InvalidOperationException>();
        
        var assetBeforeProcessing = new AssetBeforeProcessing(new Asset()
        {
            Id = new AssetId(1, 1, "asset"),
            MediaType = "image/jpg",
            Origin = "https://some/origin"
        }, new []
        {
            new DeliveryChannelsBeforeProcessing("iiif-img", "somePolicy"),
            new DeliveryChannelsBeforeProcessing("thumbs", "somePolicy")
        });
        
        // Act
        var result = await sut.Process(assetBeforeProcessing, false, false, false);
        
        // Assert
        result.Result.IsSuccess.Should().BeFalse();
        result.Result.Error.Should().Be("Failed to match delivery channel policy");
    }
    
    [Fact]
    public async Task Process_ProcessesImage_WhenDeliveryPolicyMatchedFromChannel()
    {
        // Arrange
        A.CallTo(() => assetRepository.GetAsset(A<AssetId>._, A<bool>._, A<bool>._)).Returns<Asset?>(null);

        A.CallTo(() => storageRepository.GetStorageMetrics(A<int>._, A<CancellationToken>._))
            .Returns(new AssetStorageMetric
            {
                Policy = new StoragePolicy{MaximumTotalSizeOfStoredImages = 1000, MaximumNumberOfStoredImages = 10000},
                CustomerStorage = new CustomerStorage{ TotalSizeOfStoredImages = 10}
            });

        var assetBeforeProcessing = new AssetBeforeProcessing(new Asset()
        {
            Id = new AssetId(1, 1, "asset"),
            MediaType = "image/jpg",
            Origin = "https://some/origin"
        }, new []
        {
            new DeliveryChannelsBeforeProcessing("iiif-img", null)
        });
        
        // Act
        var result = await sut.Process(assetBeforeProcessing, false, false, false);
        
        // Assert
        result.Result.IsSuccess.Should().BeTrue();

        A.CallTo(() => defaultDeliveryChannelRepository.MatchDeliveryChannelPolicyForChannel(A<string>._, A<int>._, A<int>._, A<string>._))
            .MustHaveHappened();
    }
    
    [Fact]
    public async Task Process_FailsToProcessesImage_WhenDeliveryPolicyNotMatchedFromChannel()
    {
        // Arrange
        A.CallTo(() => assetRepository.GetAsset(A<AssetId>._, A<bool>._, A<bool>._)).Returns<Asset?>(null);

        A.CallTo(() => storageRepository.GetStorageMetrics(A<int>._, A<CancellationToken>._))
            .Returns(new AssetStorageMetric
            {
                Policy = new StoragePolicy{MaximumTotalSizeOfStoredImages = 1000, MaximumNumberOfStoredImages = 10000},
                CustomerStorage = new CustomerStorage{ TotalSizeOfStoredImages = 10}
            });
        
        A.CallTo(() => defaultDeliveryChannelRepository.MatchDeliveryChannelPolicyForChannel(A<string>._, A<int>._, A<int>._, A<string>._))
            .Throws<InvalidOperationException>();
        
        var assetBeforeProcessing = new AssetBeforeProcessing(new Asset()
        {
            Id = new AssetId(1, 1, "asset"),
            MediaType = "image/jpg",
            Origin = "https://some/origin"
        }, new []
        {
            new DeliveryChannelsBeforeProcessing("iiif-img", null)
        });
        
        // Act
        var result = await sut.Process(assetBeforeProcessing, false, false, false);
        
        // Assert
        result.Result.IsSuccess.Should().BeFalse();
        result.Result.Error.Should().Be("Failed to match delivery channel policy");
    }
}