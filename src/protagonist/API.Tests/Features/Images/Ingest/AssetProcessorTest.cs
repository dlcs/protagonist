using System.Threading;
using System.Threading.Tasks;
using API.Features.Assets;
using API.Features.Image.Ingest;
using API.Settings;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Policies;
using DLCS.Model.Storage;
using FakeItEasy;
using Test.Helpers.Settings;

namespace API.Tests.Features.Images.Ingest;

public class AssetProcessorTest
{
    private readonly AssetProcessor sut;
    private readonly IPolicyRepository policyRepository;
    private readonly IApiAssetRepository assetRepository;
    private readonly IStorageRepository storageRepository;

    public AssetProcessorTest()
    {
        var apiSettings = new ApiSettings();
        storageRepository = A.Fake<IStorageRepository>();
        policyRepository = A.Fake<IPolicyRepository>();
        assetRepository = A.Fake<IApiAssetRepository>();
        
        var optionsMonitor = OptionsHelpers.GetOptionsMonitor(apiSettings);

        sut = new AssetProcessor(assetRepository, storageRepository, policyRepository, optionsMonitor);
    }
    
    [Fact]
    public async Task Process_ChecksForMaximumNumberOfImages_Exceeded()
    {
        // Arrange
        A.CallTo(() => assetRepository.GetAsset(A<AssetId>._, A<bool>._)).Returns<Asset?>(null);

        A.CallTo(() => storageRepository.GetStorageMetrics(A<int>._, A<CancellationToken>._))
            .Returns(new AssetStorageMetric
            {
                Policy = new StoragePolicy{MaximumTotalSizeOfStoredImages = 1000, MaximumNumberOfStoredImages = 0},
                CustomerStorage = new CustomerStorage{ TotalSizeOfStoredImages = 10}
            });

        var asset = new Asset();
        
        // Act
        var result = await sut.Process(asset, false, false, false);
        
        // Assert
        result.Result.IsSuccess.Should().BeFalse();
        result.Result.Error.Should().Be("This operation will fall outside of your storage policy for number of images: maximum is 0");
    }
    
    [Fact]
    public async Task Process_ChecksForTotalImageSize_Exceeded()
    {
        // Arrange
        A.CallTo(() => assetRepository.GetAsset(A<AssetId>._, A<bool>._)).Returns<Asset?>(null);

        A.CallTo(() => storageRepository.GetStorageMetrics(A<int>._, A<CancellationToken>._))
            .Returns(new AssetStorageMetric
            {
                Policy = new StoragePolicy{MaximumTotalSizeOfStoredImages = -1, MaximumNumberOfStoredImages = 10},
                CustomerStorage = new CustomerStorage{ TotalSizeOfStoredImages = 10}
            });

        var asset = new Asset();
        
        // Act
        var result = await sut.Process(asset, false, false, false);
        
        // Assert
        result.Result.IsSuccess.Should().BeFalse();
        result.Result.Error.Should().Be("The total size of stored images has exceeded your allowance: maximum is -1");
    }
}