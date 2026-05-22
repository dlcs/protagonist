using System.Text;
using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Customers;
using DLCS.Repository.Strategy;
using DLCS.Repository.Strategy.DependencyInjection;
using DLCS.Repository.Strategy.Utils;
using Engine.Ingest;
using Engine.Ingest.File;
using Engine.Ingest.Persistence;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;

namespace Engine.Tests.Ingest.File;

public class FileChannelWorkerTests
{
    private readonly IAssetToS3 assetToS3;
    private readonly IStorageKeyGenerator storageKeyGenerator;
    private readonly IOriginStrategy originStrategy;
    private readonly FileChannelWorker sut;

    public FileChannelWorkerTests()
    {
        var assetIngestorSizeCheck = new HardcodedAssetIngestorSizeCheckBase(10);
        assetToS3 = A.Fake<IAssetToS3>();
        storageKeyGenerator = A.Fake<IStorageKeyGenerator>();
        originStrategy = A.Fake<IOriginStrategy>();
        OriginStrategyResolver resolver = _ => originStrategy;
        var originFetcher = new OriginFetcher(null, resolver);

        sut = new FileChannelWorker(assetToS3, assetIngestorSizeCheck, storageKeyGenerator, originFetcher,
            new NullLogger<FileChannelWorker>());
    }

    [Fact]
    public async Task Ingest_NoOp_IfOptimisedStrategy()
    {
        // Arrange
        var context = GetAssetIngestionContext();
        var cos = new CustomerOriginStrategy { Strategy = OriginStrategyType.S3Ambient, Optimised = true };
        
        // Act
        var result = await sut.Ingest(context, cos);
        
        // Assert
        result.Should().Be(IngestResultStatus.Success);
        A.CallTo(() =>
                assetToS3.CopyOriginToStorage(A<ObjectInBucket>._, A<IngestionContext>._, A<bool>._, cos, A<Func<string, CancellationToken, Task<string?>>>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task Ingest_CopiesFileToStorage_SetsImageStorage_AndStoredObject()
    {
        // Arrange
        var context = GetAssetIngestionContext();
        
        var cos = new CustomerOriginStrategy { Strategy = OriginStrategyType.S3Ambient, Optimised = false };
        var destination = new RegionalisedObjectInBucket("test-bucket", "origin-key", "eu-west-1");
        A.CallTo(() => storageKeyGenerator.GetStoredOriginalLocation(context.AssetId))
            .Returns(destination);

        A.CallTo(() =>
                assetToS3.CopyOriginToStorage(destination, context, true, cos, A<Func<string, CancellationToken, Task<string?>>>._, A<CancellationToken>._))
            .Returns(new AssetFromOrigin(context.AssetId, 1234L, "anywhere", "application/docx"));

        // Act
        var result = await sut.Ingest(context, cos);
        
        // Assert
        context.ImageStorage!.Size.Should().Be(1234L);
        context.StoredObjects.Should().ContainKey(destination).WhoseValue.Should().Be(1234L);
        result.Should().Be(IngestResultStatus.Success);
    }
    
    [Fact]
    public async Task Ingest_CopiesFileToStorage_IncrementsImageStorage_AndStoredObject()
    {
        // Arrange
        var context = GetAssetIngestionContext();
        context.WithStorage(assetSize: 1000L);
        
        var cos = new CustomerOriginStrategy { Strategy = OriginStrategyType.S3Ambient, Optimised = false };
        var destination = new RegionalisedObjectInBucket("test-bucket", "origin-key", "eu-west-1");
        A.CallTo(() => storageKeyGenerator.GetStoredOriginalLocation(context.AssetId))
            .Returns(destination);

        A.CallTo(() =>
                assetToS3.CopyOriginToStorage(destination, context, true, cos, A<Func<string, CancellationToken, Task<string?>>>._, A<CancellationToken>._))
            .Returns(new AssetFromOrigin(context.AssetId, 1234L, "anywhere", "application/docx"));

        // Act
        var result = await sut.Ingest(context, cos);
        
        // Assert
        context.ImageStorage!.Size.Should().Be(2234L, "Was 1000 from previous operation");
        context.StoredObjects.Should().ContainKey(destination).WhoseValue.Should().Be(1234L);
        result.Should().Be(IngestResultStatus.Success);
    }

    [Fact]
    public async Task Ingest_ReturnsErrorIfCopyExceedStorageLimit()
    {
        // Arrange
        var context = GetAssetIngestionContext();
        
        var cos = new CustomerOriginStrategy { Strategy = OriginStrategyType.S3Ambient, Optimised = false };
        var destination = new RegionalisedObjectInBucket("test-bucket", "origin-key", "eu-west-1");
        A.CallTo(() => storageKeyGenerator.GetStoredOriginalLocation(context.AssetId))
            .Returns(destination);

        var assetFromOrigin = new AssetFromOrigin(context.AssetId, 1234L, "anywhere", "application/docx");
        assetFromOrigin.FileTooLarge();
        A.CallTo(() =>
                assetToS3.CopyOriginToStorage(destination, context, true, cos, A<Func<string, CancellationToken, Task<string?>>>._, A<CancellationToken>._))
            .Returns(assetFromOrigin);

        // Act
        var result = await sut.Ingest(context, cos);
        
        // Assert
        context.ImageStorage.Should().BeNull();
        context.Asset.Error.Should().Be("StoragePolicy size limit exceeded");
        result.Should().Be(IngestResultStatus.StorageLimitExceeded);
    }
    
    [Fact]
    public async Task Ingest_CopiesFileToStorage_PassesVerifySizeFalse_IfCustomerExcluded()
    {
        // Arrange
        var context = GetAssetIngestionContext("/10/2/something");

        var cos = new CustomerOriginStrategy { Strategy = OriginStrategyType.S3Ambient, Optimised = false };
        var destination = new RegionalisedObjectInBucket("test-bucket", "origin-key", "eu-west-1");
        A.CallTo(() => storageKeyGenerator.GetStoredOriginalLocation(context.AssetId))
            .Returns(destination);

        // Act
        var result = await sut.Ingest(context, cos);
        
        // Assert
        A.CallTo(() =>
                assetToS3.CopyOriginToStorage(destination, context, false, cos, A<Func<string, CancellationToken, Task<string?>>>._, A<CancellationToken>._))
            .MustHaveHappened();
        result.Should().Be(IngestResultStatus.Success);
    }
    
    [Fact]
    public async Task Ingest_ReturnsFailedState_IfErrorThrown()
    {
        // Arrange
        var context = GetAssetIngestionContext("/10/2/something");

        var cos = new CustomerOriginStrategy { Strategy = OriginStrategyType.S3Ambient, Optimised = false };
        A.CallTo(() => storageKeyGenerator.GetStoredOriginalLocation(context.AssetId))
            .Throws(new ApplicationException("I am an error"));

        // Act
        var result = await sut.Ingest(context, cos);
        
        // Assert
        context.Asset.Error.Should().Be("I am an error");
        result.Should().Be(IngestResultStatus.Failed);
    }
    
    // Adjuncts
    
    [Fact]
    public async Task IngestAdjunct_NoOp_IfOptimisedStrategy()
    {
        // Arrange
        var context = GetAdjunctIngestionContext();
        var cos = new CustomerOriginStrategy { Strategy = OriginStrategyType.S3Ambient, Optimised = true };
        
        // Act
        var result = await sut.Ingest(context, cos);
        
        // Assert
        result.Should().Be(IngestResultStatus.Success);
        A.CallTo(() =>
                assetToS3.CopyOriginToStorage(A<ObjectInBucket>._, A<IngestionContext>._, A<bool>._, cos, A<Func<string, CancellationToken, Task<string?>>>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task IngestAdjunct_CopiesFileToStorage_SetsImageStorage_AndStoredObject()
    {
        // Arrange
        var context = GetAdjunctIngestionContext();
        
        var cos = new CustomerOriginStrategy { Strategy = OriginStrategyType.S3Ambient, Optimised = false };
        var destination = new RegionalisedObjectInBucket("test-bucket", "origin-key", "eu-west-1");
        A.CallTo(() => storageKeyGenerator.GetStoredAdjunctLocation(context.AssetId, context.Adjunct))
            .Returns(destination);

        A.CallTo(() =>
                assetToS3.CopyOriginToStorage(destination, context, true, cos, A<Func<string, CancellationToken, Task<string?>>>._, A<CancellationToken>._))
            .Returns(new AdjunctFromOrigin(context.Adjunct.Id, context.AssetId, 1234L, "anywhere", "application/docx"));

        // Act
        var result = await sut.Ingest(context, cos);
        
        // Assert
        context.ImageStorage!.AdjunctSize.Should().Be(1234L);
        context.StoredObjects.Should().ContainKey(destination).WhoseValue.Should().Be(1234L);
        result.Should().Be(IngestResultStatus.Success);
    }
    
    [Fact]
    public async Task IngestAdjunct_CopiesFileToStorage_IncrementsImageStorage_AndStoredObject()
    {
        // Arrange
        var context = GetAdjunctIngestionContext();
        context.WithStorage(adjunctSize: 1000L);
        
        var cos = new CustomerOriginStrategy { Strategy = OriginStrategyType.S3Ambient, Optimised = false };
        var destination = new RegionalisedObjectInBucket("test-bucket", "origin-key", "eu-west-1");
        A.CallTo(() => storageKeyGenerator.GetStoredAdjunctLocation(context.AssetId, context.Adjunct))
            .Returns(destination);

        A.CallTo(() =>
                assetToS3.CopyOriginToStorage(destination, context, true, cos, A<Func<string, CancellationToken, Task<string?>>>._, A<CancellationToken>._))
            .Returns(new AdjunctFromOrigin(context.Adjunct.Id, context.AssetId, 1234L, "anywhere", "application/docx"));

        // Act
        var result = await sut.Ingest(context, cos);
        
        // Assert
        context.ImageStorage!.AdjunctSize.Should().Be(2234L, "Was 1000 from previous operation");
        context.StoredObjects.Should().ContainKey(destination).WhoseValue.Should().Be(1234L);
        result.Should().Be(IngestResultStatus.Success);
    }
    
    [Fact]
    public async Task IngestAdjunct_ReturnsErrorIfCopyExceedStorageLimit()
    {
        // Arrange
        var context = GetAdjunctIngestionContext();
        
        var cos = new CustomerOriginStrategy { Strategy = OriginStrategyType.S3Ambient, Optimised = false };
        var destination = new RegionalisedObjectInBucket("test-bucket", "origin-key", "eu-west-1");
        A.CallTo(() => storageKeyGenerator.GetStoredAdjunctLocation(context.AssetId, context.Adjunct))
            .Returns(destination);

        var assetFromOrigin = new AdjunctFromOrigin(context.Adjunct.Id, context.AssetId, 1234L, "anywhere", "application/docx");
        assetFromOrigin.FileTooLarge();
        
        A.CallTo(() =>
                assetToS3.CopyOriginToStorage(destination, context, true, cos, A<Func<string, CancellationToken, Task<string?>>>._, A<CancellationToken>._))
            .Returns(assetFromOrigin);

        // Act
        var result = await sut.Ingest(context, cos);
        
        // Assert
        context.ImageStorage.Should().BeNull();
        context.Adjunct.Error.Should().Be("StoragePolicy size limit exceeded");
        result.Should().Be(IngestResultStatus.StorageLimitExceeded);
    }
    
    [Fact]
    public async Task IngestAdjunct_CopiesFileToStorage_PassesVerifySizeFalse_IfCustomerExcluded()
    {
        // Arrange
        var context = GetAdjunctIngestionContext("/10/2/something");

        var cos = new CustomerOriginStrategy { Strategy = OriginStrategyType.S3Ambient, Optimised = false };
        var destination = new RegionalisedObjectInBucket("test-bucket", "origin-key", "eu-west-1");
        A.CallTo(() => storageKeyGenerator.GetStoredAdjunctLocation(context.AssetId, context.Adjunct))
            .Returns(destination);

        // Act
        var result = await sut.Ingest(context, cos);
        
        // Assert
        A.CallTo(() =>
                assetToS3.CopyOriginToStorage(destination, context, false, cos, A<Func<string, CancellationToken, Task<string?>>>._, A<CancellationToken>._))
            .MustHaveHappened();
        result.Should().Be(IngestResultStatus.Success);
    }
    
    [Fact]
    public async Task IngestAdjunct_ReturnsFailedState_IfErrorThrown()
    {
        // Arrange
        var context = GetAdjunctIngestionContext("/10/2/something");

        var cos = new CustomerOriginStrategy { Strategy = OriginStrategyType.S3Ambient, Optimised = false };
        A.CallTo(() => storageKeyGenerator.GetStoredAdjunctLocation(context.AssetId, context.Adjunct))
            .Throws(new ApplicationException("I am an error"));

        // Act
        var result = await sut.Ingest(context, cos);
        
        // Assert
        context.Adjunct.Error.Should().Be("I am an error");
        result.Should().Be(IngestResultStatus.Failed);
    }
    
    
    // Helpers
    
    private static AdjunctIngestionContext GetAdjunctIngestionContext(string assetId = "/1/2/something", string adjunctId = "someAdjunct", ImageStorage? imageStorage = null)
    {
        var id = AssetId.FromString(assetId);
        var asset = new Asset
        {
            Id = id, Customer = id.Customer, Space = id.Space,
            DeliveryChannels = [AssetDeliveryChannels.File]
        };

        var adjunct = new Adjunct
        {
            Id = adjunctId, AssetId = id, Asset = asset, IIIFLink = IIIFLinkType.SeeAlso,
            MediaType = "image/jpeg", Type = "Image"
        };

        var context = new AdjunctIngestionContext(adjunct, imageStorage);
        return context;
    }

    private static AdjunctIngestionContext GetAnnotationAdjunctIngestionContext(string assetId = "/1/2/something",
        string adjunctId = "someAdjunct", string origin = "s3://test-bucket/annotation-key")
    {
        var id = AssetId.FromString(assetId);
        var asset = new Asset
        {
            Id = id, Customer = id.Customer, Space = id.Space,
            DeliveryChannels = [AssetDeliveryChannels.File]
        };

        var adjunct = new Adjunct
        {
            Id = adjunctId, AssetId = id, Asset = asset, IIIFLink = IIIFLinkType.Annotations,
            MediaType = "application/json", Type = "AnnotationPage", Origin = origin
        };

        return new AdjunctIngestionContext(adjunct, null);
    }

    private static OriginResponse MakeOriginResponse(string content)
        => new(new MemoryStream(Encoding.UTF8.GetBytes(content)));

    // Annotation adjuncts - Optimised path (validate via OriginFetcher, no S3 copy)

    [Fact]
    public async Task IngestAdjunct_Annotation_Optimised_ValidJson_ReturnsSuccess()
    {
        // Arrange
        var context = GetAnnotationAdjunctIngestionContext();
        var cos = new CustomerOriginStrategy { Strategy = OriginStrategyType.S3Ambient, Optimised = true };
        A.CallTo(() => originStrategy.LoadFromOrigin(context.Adjunct, cos, A<CancellationToken>._))
            .Returns(MakeOriginResponse("""{"type":"AnnotationPage"}"""));

        // Act
        var result = await sut.Ingest(context, cos);

        // Assert
        result.Should().Be(IngestResultStatus.Success);
        A.CallTo(() =>
                assetToS3.CopyOriginToStorage(A<ObjectInBucket>._, A<IngestionContext>._, A<bool>._, cos,
                    A<Func<string, CancellationToken, Task<string?>>>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task IngestAdjunct_Annotation_Optimised_InvalidJson_ReturnsFailed()
    {
        // Arrange
        var context = GetAnnotationAdjunctIngestionContext();
        var cos = new CustomerOriginStrategy { Strategy = OriginStrategyType.S3Ambient, Optimised = true };
        A.CallTo(() => originStrategy.LoadFromOrigin(context.Adjunct, cos, A<CancellationToken>._))
            .Returns(MakeOriginResponse("not valid json {{"));

        // Act
        var result = await sut.Ingest(context, cos);

        // Assert
        result.Should().Be(IngestResultStatus.Failed);
        context.Adjunct.Error.Should().Be("Annotation content is not valid JSON");
        A.CallTo(() =>
                assetToS3.CopyOriginToStorage(A<ObjectInBucket>._, A<IngestionContext>._, A<bool>._, cos,
                    A<Func<string, CancellationToken, Task<string?>>>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task IngestAdjunct_Annotation_Optimised_EmptyResponse_ReturnsFailed()
    {
        // Arrange
        var context = GetAnnotationAdjunctIngestionContext();
        var cos = new CustomerOriginStrategy { Strategy = OriginStrategyType.S3Ambient, Optimised = true };
        A.CallTo(() => originStrategy.LoadFromOrigin(context.Adjunct, cos, A<CancellationToken>._))
            .Returns(OriginResponse.Empty);

        // Act
        var result = await sut.Ingest(context, cos);

        // Assert
        result.Should().Be(IngestResultStatus.Failed);
        context.Adjunct.Error.Should().Be("Unable to read annotation content for validation");
    }

    // Annotation adjuncts - non-Optimised path (validator callback passed to CopyOriginToStorage)

    [Fact]
    public async Task IngestAdjunct_Annotation_NonOptimised_ValidJson_ReturnsSuccess()
    {
        // Arrange
        var context = GetAnnotationAdjunctIngestionContext();
        var cos = new CustomerOriginStrategy { Strategy = OriginStrategyType.S3Ambient, Optimised = false };
        var destination = new RegionalisedObjectInBucket("test-bucket", "annotation-key", "eu-west-1");
        A.CallTo(() => storageKeyGenerator.GetStoredAdjunctLocation(context.AssetId, context.Adjunct))
            .Returns(destination);
        A.CallTo(() =>
                assetToS3.CopyOriginToStorage(destination, context, true, cos,
                    A<Func<string, CancellationToken, Task<string?>>>._, A<CancellationToken>._))
            .Returns(new AdjunctFromOrigin(context.Adjunct.Id, context.AssetId, 512L, "anywhere", "application/json"));

        // Act
        var result = await sut.Ingest(context, cos);

        // Assert
        result.Should().Be(IngestResultStatus.Success);
        A.CallTo(() =>
                assetToS3.CopyOriginToStorage(destination, context, true, cos,
                    A<Func<string, CancellationToken, Task<string?>>>.That.IsNotNull(), A<CancellationToken>._))
            .MustHaveHappened();
    }

    [Fact]
    public async Task IngestAdjunct_Annotation_NonOptimised_InvalidJson_ReturnsFailed()
    {
        // Arrange
        var context = GetAnnotationAdjunctIngestionContext();
        var cos = new CustomerOriginStrategy { Strategy = OriginStrategyType.S3Ambient, Optimised = false };
        var destination = new RegionalisedObjectInBucket("test-bucket", "annotation-key", "eu-west-1");
        A.CallTo(() => storageKeyGenerator.GetStoredAdjunctLocation(context.AssetId, context.Adjunct))
            .Returns(destination);
        A.CallTo(() =>
                assetToS3.CopyOriginToStorage(destination, context, true, cos,
                    A<Func<string, CancellationToken, Task<string?>>>._, A<CancellationToken>._))
            .Throws(new InvalidOperationException("Annotation content is not valid JSON"));

        // Act
        var result = await sut.Ingest(context, cos);

        // Assert
        result.Should().Be(IngestResultStatus.Failed);
        context.Adjunct.Error.Should().Be("Annotation content is not valid JSON");
    }
    
    private static IngestionContext GetAssetIngestionContext(string assetId = "/1/2/something")
    {
        var id = AssetId.FromString(assetId);
        var asset = new Asset
        {
            Id = id, Customer = id.Customer, Space = id.Space,
            DeliveryChannels = [AssetDeliveryChannels.File]
        };
        
        var context = new IngestionContext(asset);
        return context;
    }
}
