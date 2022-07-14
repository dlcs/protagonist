using DLCS.Model.Assets;
using DLCS.Model.Customers;
using DLCS.Model.Storage;
using DLCS.Repository.Strategy;
using DLCS.Repository.Strategy.Utils;
using Engine.Ingest.Workers;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using Test.Helpers;

namespace Engine.Tests.Ingest.Workers;

[Trait("Requires", "FileAccess")]
public class AssetToDiskTests
{
    private readonly AssetToDisk sut;
    private readonly IOriginStrategy customerOriginStrategy;
    private readonly IStorageRepository customerStorageRepository;

    public AssetToDiskTests()
    {
        customerStorageRepository = A.Fake<IStorageRepository>();

        // For unit-test only s3ambient will be mocked
        customerOriginStrategy = A.Fake<IOriginStrategy>();
        A.CallTo(() => customerOriginStrategy.Strategy).Returns(OriginStrategyType.S3Ambient);
        var originStrategies = new[] { customerOriginStrategy };
        var fileSaver = new FileSaver(new NullLogger<FileSaver>());

        sut = new AssetToDisk(originStrategies, customerStorageRepository, fileSaver, new NullLogger<AssetToDisk>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void CopyAssetFromOrigin_Throws_IfDestinationFolderNullOrEmpty(string destinationFolder)
    {
        // Act
        Func<Task> action = () => sut.CopyAsset(new Asset(), destinationFolder, true, new CustomerOriginStrategy());

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
        Func<Task> action = () => sut.CopyAsset(asset, "./here", true, cos);

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
        Func<Task> action = () => sut.CopyAsset(asset, "./here", true, cos);

        // Assert
        action.Should().ThrowAsync<ApplicationException>();
    }

    [Fact]
    [Trait("Requires", "FileAccess")]
    public async Task CopyAssetFromOrigin_SavesFileToDisk_IfNoContentLength()
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

        Directory.CreateDirectory(Path.Join(".", "2", "1", "godzilla"));
        var expectedOutput = Path.Join(".", "2", "1", "godzilla", "godzilla.file");

        // Act
        var response = await sut.CopyAsset(asset, destination, false, cos);

        // Assert
        File.Exists(expectedOutput).Should().BeTrue();
        File.Delete(expectedOutput);
        response.Location.Should().Be(expectedOutput);
        response.ContentType.Should().Be("application/json");
        response.AssetSize.Should().BeGreaterThan(0);
        response.AssetId.Should().Be(asset.Id);
        response.CustomerOriginStrategy.Should().Be(cos);
    }

    [Fact]
    [Trait("Requires", "FileAccess")]
    public async Task CopyAssetFromOrigin_SavesFileToDisk_IfContentLength()
    {
        // Arrange
        var destination = Path.Join(".", "2", "1", "godzilla1");
        const string origin = "http://test-origin";
        var asset = new Asset { Id = "/2/1/godzilla1", Customer = 2, Space = 1, Origin = origin };
        var cos = new CustomerOriginStrategy { Strategy = OriginStrategyType.S3Ambient };

        var responseStream = "{\"foo\":\"bar\"}".ToMemoryStream();
        var originResponse = new OriginResponse(responseStream)
            .WithContentType("application/json")
            .WithContentLength(8);
        A.CallTo(() =>
                customerOriginStrategy.LoadAssetFromOrigin(asset.GetAssetId(), origin, cos, A<CancellationToken>._))
            .Returns(originResponse);

        Directory.CreateDirectory(Path.Join(".", "2", "1", "godzilla1"));
        var expectedOutput = Path.Join(".", "2", "1", "godzilla1", "godzilla1.file");

        // Act
        var response = await sut.CopyAsset(asset, destination, false, cos);

        // Assert
        File.Exists(expectedOutput).Should().BeTrue();
        File.Delete(expectedOutput);
        response.Location.Should().Be(expectedOutput);
        response.ContentType.Should().Be("application/json");
        response.AssetSize.Should().Be(8);
        response.AssetId.Should().Be(asset.Id);
        response.CustomerOriginStrategy.Should().Be(cos);
    }

    [Theory]
    [InlineData("image/jpg", "jpg")]
    [InlineData("application/pdf", "pdf")]
    [InlineData("gibberish", "file")]
    [Trait("Requires", "FileAccess")]
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

        Directory.CreateDirectory(Path.Join(".", "2", "1", "godzilla.jp2"));
        var expectedOutput = Path.Join(".", "2", "1", "godzilla.jp2", $"godzilla.jp2.{extension}");

        // Act
        var response = await sut.CopyAsset(asset, destination, false, cos);

        // Assert
        File.Exists(expectedOutput).Should().BeTrue();
        File.Delete(expectedOutput);
        response.ContentType.Should().Be(contentType);
    }

    [Theory]
    [InlineData("")]
    [InlineData("application/octet-stream")]
    [InlineData("binary/octet-stream")]
    [Trait("Requires", "FileAccess")]
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

        Directory.CreateDirectory(Path.Join(".", "2", "1", "godzilla.jp2"));
        var expectedOutput = Path.Join(".", "2", "1", "godzilla.jp2", "godzilla.jp2.jp2");

        // Act
        var response = await sut.CopyAsset(asset, destination, false, cos);

        // Assert
        File.Delete(expectedOutput);
        response.ContentType.Should().Be("image/jp2");
    }

    [Theory]
    [Trait("Requires", "FileAccess")]
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

        Directory.CreateDirectory(Path.Join(".", "2", "1", "godzilla"));
        var expectedOutput = Path.Join(".", "2", "1", "godzilla", "godzilla.file");

        A.CallTo(() => customerStorageRepository.VerifyStoragePolicyBySize(2, A<long>._, A<CancellationToken>._))
            .Returns(isValid);

        // Act
        var response = await sut.CopyAsset(asset, destination, true, cos);

        // Assert
        File.Exists(expectedOutput).Should().BeTrue();
        File.Delete(expectedOutput);
        response.FileExceedsAllowance.Should().Be(!isValid);
    }
}