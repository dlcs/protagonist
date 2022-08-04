using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Customers;
using DLCS.Model.Storage;
using DLCS.Repository.Strategy;
using DLCS.Repository.Strategy.DependencyInjection;
using DLCS.Repository.Strategy.Utils;
using Engine.Ingest.Persistence;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using Test.Helpers;

namespace Engine.Tests.Ingest.Persistence;

public class AssetToDiskTests
{
    private readonly AssetToDisk sut;
    private readonly IOriginStrategy customerOriginStrategy;
    private readonly IStorageRepository customerStorageRepository;
    private readonly IFileSaver fileSaver;

    public AssetToDiskTests()
    {
        customerStorageRepository = A.Fake<IStorageRepository>();
        fileSaver = A.Fake<IFileSaver>();

        // For unit-test only s3ambient will be mocked
        customerOriginStrategy = A.Fake<IOriginStrategy>();
        OriginStrategyResolver resolver = _ => customerOriginStrategy;
        
        var originFetched = new OriginFetcher(null, resolver);

        sut = new AssetToDisk(originFetched, customerStorageRepository, fileSaver, new NullLogger<AssetToDisk>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void CopyAssetFromOrigin_Throws_IfDestinationFolderNullOrEmpty(string destinationFolder)
    {
        // Act
        Func<Task> action = () => sut.CopyAssetToLocalDisk(new Asset(), destinationFolder, true, new CustomerOriginStrategy());

        // Assert
        action.Should()
            .ThrowAsync<ArgumentNullException>()
            .WithMessage("Value cannot be null. (Parameter 'destinationTemplate')");
    }

    [Fact]
    public void CopyAssetFromOrigin_Throws_IfOriginReturnsNull()
    {
        // Arrange
        const string origin = "http://test-origin";
        var asset = new Asset { Id = "/2/1/godzilla" };
        var cos = new CustomerOriginStrategy { Strategy = OriginStrategyType.S3Ambient };
        A.CallTo(() =>
                customerOriginStrategy.LoadAssetFromOrigin(asset.GetAssetId(), origin, cos, A<CancellationToken>._))
            .Returns<OriginResponse?>(null);

        // Act
        Func<Task> action = () => sut.CopyAssetToLocalDisk(asset, "./here", true, cos);

        // Assert
        action.Should().ThrowAsync<ApplicationException>();
    }

    [Fact]
    public void CopyAssetFromOrigin_Throws_IfOriginReturnsEmptyStream()
    {
        // Arrange
        const string origin = "http://test-origin";
        var asset = new Asset { Id = "/2/1/godzilla", Origin = origin};
        var cos = new CustomerOriginStrategy { Strategy = OriginStrategyType.S3Ambient };
        A.CallTo(() =>
                customerOriginStrategy.LoadAssetFromOrigin(asset.GetAssetId(), origin, cos, A<CancellationToken>._))
            .Returns(new OriginResponse(Stream.Null));

        // Act
        Func<Task> action = () => sut.CopyAssetToLocalDisk(asset, "./here", true, cos);

        // Assert
        action.Should().ThrowAsync<ApplicationException>();
    }

    [Fact]
    public async Task CopyAssetFromOrigin_SavesFileToDisk_IfNoContentLength()
    {
        // Arrange
        var destination = Path.Join(".", "2", "1", "godzilla");
        const string origin = "http://test-origin";
        var asset = new Asset { Id = "/2/1/godzilla", Customer = 2, Space = 1, Origin = origin };
        AssetId assetId = AssetId.FromString(asset.Id);
        var cos = new CustomerOriginStrategy { Strategy = OriginStrategyType.S3Ambient };

        var responseStream = "{\"foo\":\"bar\"}".ToMemoryStream();
        var originResponse = new OriginResponse(responseStream).WithContentType("application/json");
        A.CallTo(() =>
                customerOriginStrategy.LoadAssetFromOrigin(asset.GetAssetId(), origin, cos, A<CancellationToken>._))
            .Returns(originResponse);
        const long fileLength = 224L;
        A.CallTo(() => fileSaver.SaveResponseToDisk(A<AssetId>._, originResponse, A<string>._, A<CancellationToken>._))
            .Returns(fileLength);

        var expectedOutput = Path.Join(".", "2", "1", "godzilla", "godzilla.file");

        // Act
        var response = await sut.CopyAssetToLocalDisk(asset, destination, false, cos);

        // Assert
        A.CallTo(() => fileSaver.SaveResponseToDisk(A<AssetId>.That.Matches(a => a == assetId),
            originResponse, A<string>._, A<CancellationToken>._)).MustHaveHappened();
        response.Location.Should().Be(expectedOutput);
        response.ContentType.Should().Be("application/json");
        response.AssetSize.Should().Be(fileLength);
        response.AssetId.Should().Be(assetId);
        response.CustomerOriginStrategy.Should().Be(cos);
    }

    [Fact]
    public async Task CopyAssetFromOrigin_SavesFileToDisk_IfContentLength()
    {
        // Arrange
        var destination = Path.Join(".", "2", "1", "godzilla1");
        const string origin = "http://test-origin";
        var asset = new Asset { Id = "/2/1/godzilla1", Customer = 2, Space = 1, Origin = origin };
        AssetId assetId = AssetId.FromString(asset.Id);
        var cos = new CustomerOriginStrategy { Strategy = OriginStrategyType.S3Ambient };

        var responseStream = "{\"foo\":\"bar\"}".ToMemoryStream();
        var originResponse = new OriginResponse(responseStream)
            .WithContentType("application/json")
            .WithContentLength(8);
        A.CallTo(() =>
                customerOriginStrategy.LoadAssetFromOrigin(asset.GetAssetId(), origin, cos, A<CancellationToken>._))
            .Returns(originResponse);
        const long fileLength = 224L;
        A.CallTo(() => fileSaver.SaveResponseToDisk(A<AssetId>._, originResponse, A<string>._, A<CancellationToken>._))
            .Returns(fileLength);
        
        var expectedOutput = Path.Join(".", "2", "1", "godzilla1", "godzilla1.file");

        // Act
        var response = await sut.CopyAssetToLocalDisk(asset, destination, false, cos);

        // Assert
        A.CallTo(() => fileSaver.SaveResponseToDisk(A<AssetId>.That.Matches(a => a == assetId),
            originResponse, A<string>._, A<CancellationToken>._)).MustHaveHappened();
        response.Location.Should().Be(expectedOutput);
        response.ContentType.Should().Be("application/json");
        response.AssetSize.Should().Be(fileLength);
        response.AssetId.Should().Be(assetId);
        response.CustomerOriginStrategy.Should().Be(cos);
    }

    [Theory]
    [InlineData("image/jpg", "jpg")]
    [InlineData("application/pdf", "pdf")]
    [InlineData("gibberish", "file")]
    public async Task CopyAssetFromOrigin_SetsExtension_BasedOnFileType(string contentType, string extension)
    {
        // Arrange
        var destination = Path.Join(".", "2", "1", "godzilla.jp2");
        const string origin = "http://test-origin";
        var asset = new Asset { Id = "/2/1/godzilla.jp2", Customer = 2, Space = 1, Origin = origin };
        var cos = new CustomerOriginStrategy { Strategy = OriginStrategyType.S3Ambient };

        var responseStream = "{\"foo\":\"bar\"}".ToMemoryStream();
        var originResponse = new OriginResponse(responseStream)
            .WithContentType(contentType)
            .WithContentLength(8);
        A.CallTo(() =>
                customerOriginStrategy.LoadAssetFromOrigin(asset.GetAssetId(), origin, cos, A<CancellationToken>._))
            .Returns(originResponse);

        // Act
        var response = await sut.CopyAssetToLocalDisk(asset, destination, false, cos);

        // Assert
        response.ContentType.Should().Be(contentType);
    }

    [Theory]
    [InlineData("")]
    [InlineData("application/octet-stream")]
    [InlineData("binary/octet-stream")]
    public async Task CopyAssetFromOrigin_SetsContentType_IfUnknownOrBinary_AssetIdIsJp2(string contentType)
    {
        // Arrange
        var destination = Path.Join(".", "2", "1", "godzilla.jp2");
        const string origin = "http://test-origin";
        var asset = new Asset { Id = "/2/1/godzilla.jp2", Customer = 2, Space = 1, Origin = origin };
        var cos = new CustomerOriginStrategy { Strategy = OriginStrategyType.S3Ambient };

        var responseStream = "{\"foo\":\"bar\"}".ToMemoryStream();
        var originResponse = new OriginResponse(responseStream)
            .WithContentType(contentType)
            .WithContentLength(8);
        A.CallTo(() =>
                customerOriginStrategy.LoadAssetFromOrigin(asset.GetAssetId(), origin, cos, A<CancellationToken>._))
            .Returns(originResponse);

        // Act
        var response = await sut.CopyAssetToLocalDisk(asset, destination, false, cos);

        // Assert
        response.ContentType.Should().Be("image/jp2");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CopyAssetFromOrigin_VerifiesFileSize(bool isValid)
    {
        // Arrange
        var destination = Path.Join(".", "2", "1", "godzilla");
        const string origin = "http://test-origin";
        var asset = new Asset { Id = "/2/1/godzilla", Customer = 2, Space = 1, Origin = origin };
        var cos = new CustomerOriginStrategy { Strategy = OriginStrategyType.S3Ambient };

        var responseStream = "{\"foo\":\"bar\"}".ToMemoryStream();
        var originResponse = new OriginResponse(responseStream).WithContentType("application/json");
        A.CallTo(() =>
                customerOriginStrategy.LoadAssetFromOrigin(asset.GetAssetId(), origin, cos, A<CancellationToken>._))
            .Returns(originResponse);

        A.CallTo(() => customerStorageRepository.VerifyStoragePolicyBySize(2, A<long>._, A<CancellationToken>._))
            .Returns(isValid);

        // Act
        var response = await sut.CopyAssetToLocalDisk(asset, destination, true, cos);

        // Assert
        response.FileExceedsAllowance.Should().Be(!isValid);
    }
}