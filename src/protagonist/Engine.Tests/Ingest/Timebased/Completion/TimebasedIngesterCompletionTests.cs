﻿using Amazon.ElasticTranscoder.Model;
using DLCS.AWS.ElasticTranscoder.Models;
using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.Model.Assets;
using Engine.Data;
using Engine.Ingest.Timebased.Completion;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using Test.Helpers.Data;

namespace Engine.Tests.Ingest.Timebased.Completion;

public class TimebasedIngesterCompletionTests
{
    private readonly IEngineAssetRepository engineAssetRepository;
    private readonly IStorageKeyGenerator storageKeyGenerator;
    private readonly IBucketWriter bucketWriter;

    public TimebasedIngesterCompletionTests()
    {
        engineAssetRepository = A.Fake<IEngineAssetRepository>();
        storageKeyGenerator = A.Fake<IStorageKeyGenerator>();
        bucketWriter = A.Fake<IBucketWriter>();
    }

    private TimebasedIngestorCompletion GetSut()
    {
        return new TimebasedIngestorCompletion(engineAssetRepository, storageKeyGenerator, bucketWriter,
            NullLogger<TimebasedIngestorCompletion>.Instance);
    }
    
    [Fact]
    public async Task CompleteSuccessfulIngest_False_IfAssetNotFound()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        A.CallTo(() => engineAssetRepository.GetAsset(assetId, null, A<CancellationToken>._)).Returns<Asset?>(null);
        
        // Act
        var sut = GetSut();
        var result = await sut.CompleteSuccessfulIngest(assetId, null, new TranscodeResult());
        
        // Assert
        result.Should().BeFalse();
    }
    
    [Fact]
    public async Task CompleteSuccessfulIngest_FalseAndErrorSet_IfTranscodeNotComplete()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        var asset = new Asset(assetId);
        A.CallTo(() => engineAssetRepository.GetAsset(assetId, null, A<CancellationToken>._)).Returns(asset);
        
        // Act
        var sut = GetSut();
        var result = await sut.CompleteSuccessfulIngest(assetId,
            null,
            new TranscodeResult(new TranscodedNotification
                { Input = new JobInput(), State = "ERROR", Outputs = new List<TranscodeOutput>() }));
        
        // Assert
        result.Should().BeFalse();
        asset.Error.Should().NotBeNull();
    }
    
    [Theory]
    [InlineData(LargeObjectStatus.Cancelled)]
    [InlineData(LargeObjectStatus.Error)]
    [InlineData(LargeObjectStatus.SourceNotFound)]
    [InlineData(LargeObjectStatus.Unknown)]
    public async Task CompleteSuccessfulIngest_FalseAndErrorSet_IfCopyNotFound(LargeObjectStatus status)
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        var asset = new Asset(assetId);
        A.CallTo(() => engineAssetRepository.GetAsset(assetId, 1234, A<CancellationToken>._)).Returns(asset);
        var transcodeResult = new TranscodeResult(new TranscodedNotification
        {
            Input = new JobInput(),
            State = "COMPLETED",
            Outputs = new List<TranscodeOutput>
            {
                new() { Duration = 100, Width = 123, Height = 234, Key = "path" }
            }
        });
        A.CallTo(() => bucketWriter.CopyLargeObject(A<ObjectInBucket>._, A<ObjectInBucket>._,
                A<Func<long, Task<bool>>>._, A<string?>._, A<CancellationToken>._))
            .Returns(new LargeObjectCopyResult(status));
        
        // Act
        var sut = GetSut();
        var result = await sut.CompleteSuccessfulIngest(assetId, 1234, transcodeResult);

        // Assert
        result.Should().BeFalse();
        asset.Error.Should().NotBeNull();
    }
}