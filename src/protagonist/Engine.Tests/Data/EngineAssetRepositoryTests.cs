using DLCS.AWS.SNS.Messaging;
using DLCS.Model.Assets;
using DLCS.Repository;
using Engine.Data;
using Engine.Tests.Integration.Infrastructure;
using FakeItEasy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Test.Helpers.Data;
using Test.Helpers.Integration;

namespace Engine.Tests.Data;

[Trait("Category", "Integration")]
[Collection(DatabaseCollection.CollectionName)]
public class EngineAssetRepositoryTests
{
    private readonly DlcsContext dbContext;
    private readonly DlcsContext contextForTests;
    private readonly EngineAssetRepository sut;
    private readonly IBatchCompletedNotificationSender batchCompletedNotificationSender;

    public EngineAssetRepositoryTests(DlcsDatabaseFixture dbFixture)
    {
        dbContext = dbFixture.DbContext;

        var optionsBuilder = new DbContextOptionsBuilder<DlcsContext>();
        optionsBuilder.UseNpgsql(dbFixture.ConnectionString);
        
        batchCompletedNotificationSender = A.Fake<IBatchCompletedNotificationSender>(); 
        
        contextForTests = new DlcsContext(optionsBuilder.Options);
        sut = new EngineAssetRepository(contextForTests, batchCompletedNotificationSender,
            new NullLogger<EngineAssetRepository>());
        dbFixture.CleanUp();
    }

    [Fact]
    public async Task GetAsset_Null_IfNotFound()
    {
        var assetId = AssetIdGenerator.GetAssetId();
        var asset = await sut.GetAsset(assetId, null);
        asset.Should().BeNull("Asset was not found");
    }

    [Fact]
    public async Task GetAsset_ReturnsAssetWithDeliveryChannels()
    {
        var assetId = AssetIdGenerator.GetAssetId();
        await dbContext.Images.AddTestAsset(assetId).WithTestDeliveryChannel("iiif-img");
        await dbContext.SaveChangesAsync();
        
        var asset = await sut.GetAsset(assetId, null);
        asset.ImageDeliveryChannels.Should().NotBeNullOrEmpty("DeliveryChannels are loaded");
        asset.BatchAssets.Should().BeNull("No batch Id specified");
    }
    
    [Fact]
    public async Task GetAsset_ReturnsAssetWithAssetDeliveryChannels()
    {
        var assetId = AssetIdGenerator.GetAssetId();
        await dbContext.Images.AddTestAsset(assetId).WithTestDeliveryChannel("iiif-img").WithTestThumbnailMetadata();
        await dbContext.SaveChangesAsync();
        
        var asset = await sut.GetAsset(assetId, null);
        asset.ImageDeliveryChannels.Should().NotBeNullOrEmpty("DeliveryChannels are loaded");
        asset.BatchAssets.Should().BeNull("No batch Id specified");
        asset.AssetApplicationMetadata.Should().NotBeNull("Asset application metadata are loaded");
    }
    
    [Fact]
    public async Task GetAsset_Returns_IfBatchIdSpecifiedButNoAssetBatchRecordsFound()
    {
        var assetId = AssetIdGenerator.GetAssetId();
        await dbContext.Images.AddTestAsset(assetId).WithTestDeliveryChannel("iiif-img");
        await dbContext.SaveChangesAsync();
        
        var asset = await sut.GetAsset(assetId, -101);
        asset.ImageDeliveryChannels.Should().NotBeNullOrEmpty("DeliveryChannels are loaded");
        asset.BatchAssets.Should().BeNullOrEmpty("Batch Id specified but not found");
    }
    
    [Fact]
    public async Task GetAsset_Returns_BatchAssets_ForSpecifiedBatch_IfBatchIdSpecified()
    {
        // Simulate asset belonging to multiple batches, ensure only the specified one is returned 
        var assetId = AssetIdGenerator.GetAssetId();
        const int batchId = 4004;
        const int otherBatchId = 4005;
        await dbContext.Batches.AddTestBatch(batchId);
        await dbContext.Batches.AddTestBatch(otherBatchId);
        await dbContext.BatchAssets.AddTestBatchAsset(batchId, assetId);
        await dbContext.BatchAssets.AddTestBatchAsset(otherBatchId, assetId);
        await dbContext.Images.AddTestAsset(assetId, batch: batchId * 10).WithTestDeliveryChannel("iiif-img");
        
        await dbContext.SaveChangesAsync();
        
        var asset = await sut.GetAsset(assetId, batchId);
        asset.ImageDeliveryChannels.Should().NotBeNullOrEmpty("DeliveryChannels are loaded");
        asset.BatchAssets.Should().ContainSingle(ba => ba.BatchId == batchId, "Only specified batch is returned");
    }
    
    [Fact]
    public async Task UpdateIngestedAsset_ReturnsFalse_IfError()
    {
        var assetId = AssetIdGenerator.GetAssetId();
        var entry = await dbContext.Images.AddTestAsset(assetId, width: 10, height: 20, duration: 30,
            ingesting: true, ref1: "foo", roles: "secret");
        var existingAsset = entry.Entity;
        await dbContext.SaveChangesAsync();

        // Omit required fields
        var newAsset = new Asset(assetId);
        
        // Act
        var success = await sut.UpdateIngestedDeliverable(newAsset, null, null, true);
        
        // Assert
        success.Should().BeFalse("No rows were updated but ingest is finished");
        var updatedItem = await dbContext.Images.SingleAsync(a => a.Id == assetId);
        updatedItem.Width.Should().Be(existingAsset.Width);
        updatedItem.Height.Should().Be(existingAsset.Height);
        updatedItem.Duration.Should().Be(existingAsset.Duration);
        updatedItem.Error.Should().Be(existingAsset.Error);
    }

    [Fact]
    public async Task UpdateIngestedAsset_ModifiedExistingAsset_NoBatch_Location_OrStorage()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        var entry = await dbContext.Images.AddTestAsset(assetId, width: 0, height: 0, duration: 0,
            ingesting: true, ref1: "foo", roles: "secret");
        var existingAsset = entry.Entity;
        await dbContext.SaveChangesAsync();
        
        contextForTests.Images.Attach(existingAsset);
        
        // Act
        var success = await sut.UpdateIngestedDeliverable(existingAsset, null, null, true);
        
        existingAsset.Width = 999;
        existingAsset.Height = 1000;
        existingAsset.Duration = 99;
        existingAsset.Error = "broken state";
        
        // Assert
        success.Should().BeTrue();
        
        var updatedItem = await contextForTests.Images.SingleAsync(a => a.Id == assetId);
        updatedItem.Width.Should().Be(999);
        updatedItem.Height.Should().Be(1000);
        updatedItem.Duration.Should().Be(99);
        updatedItem.Error.Should().Be("broken state");
        updatedItem.Ingesting.Should().BeFalse();
        updatedItem.Finished.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        A.CallTo(() =>
                batchCompletedNotificationSender.SendBatchCompletedMessage(
                    A<Batch>._,
                    A<CancellationToken>._))
            .MustNotHaveHappened();
    }
    
    [Fact]
    public async Task UpdateIngestedAsset_UpdatesAlreadyTrackedAsset()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        await dbContext.Images.AddTestAsset(assetId, width: 0, height: 0, duration: 0,
            ingesting: true, ref1: "foo", roles: "secret");
        await dbContext.SaveChangesAsync();

        // Get asset so that it is tracked 
        var trackedAsset = await sut.GetAsset(assetId, null);
        trackedAsset.Width = 999;
        trackedAsset.Height = 1000;
        trackedAsset.Duration = 99;
        trackedAsset.Error = "broken state";

        // Act
        var success = await sut.UpdateIngestedDeliverable(trackedAsset, null, null, true);
        
        // Assert
        trackedAsset.Should().NotBeNull();
        
        success.Should().BeTrue();
        
        var updatedItem = await dbContext.Images.SingleAsync(a => a.Id == assetId);
        updatedItem.Width.Should().Be(trackedAsset.Width);
        updatedItem.Height.Should().Be(trackedAsset.Height);
        updatedItem.Duration.Should().Be(trackedAsset.Duration);
        updatedItem.Error.Should().Be(trackedAsset.Error);
        updatedItem.Ingesting.Should().BeFalse();
        updatedItem.Finished.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        A.CallTo(() =>
                batchCompletedNotificationSender.SendBatchCompletedMessage(
                    A<Batch>._,
                    A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task UpdateIngestedAsset_ModifiedExistingAsset_NoBatch_WithLocationAndStorage_NoExistingLocationOrStorage()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        var entity = await dbContext.Images.AddTestAsset(assetId);
        var existingAsset = entity.Entity;
        await dbContext.SaveChangesAsync();
        
        var imageLocation = new ImageLocation { Id = assetId, S3 = "union-card", Nas = "wedding-coat" };
        var imageStorage = new ImageStorage
        {
            Id = assetId, Customer = 99, Space = 1, Size = 1010, CheckingInProgress = false,
            LastChecked = DateTime.UtcNow, ThumbnailSize = 2020
        };
        
        // Act
        var success = await sut.UpdateIngestedDeliverable(existingAsset, imageLocation, imageStorage, true);
        
        // Assert
        success.Should().BeTrue();
        
        var dbImageLocation = await dbContext.ImageLocations.SingleAsync(a => a.Id == assetId);
        dbImageLocation.Should().BeEquivalentTo(imageLocation);
        var dbImageStorage = await dbContext.ImageStorages.SingleAsync(a => a.Id == assetId);
        dbImageStorage.Should().BeEquivalentTo(imageStorage, opts => opts.Excluding(s => s.LastChecked));
        dbImageStorage.LastChecked.Should().BeCloseTo(imageStorage.LastChecked, TimeSpan.FromMinutes(1));
        A.CallTo(() =>
                batchCompletedNotificationSender.SendBatchCompletedMessage(
                    A<Batch>._,
                    A<CancellationToken>._))
            .MustNotHaveHappened();
    }
    
    [Fact]
    public async Task UpdateIngestedAsset_ModifiedExistingAsset_NoBatch_WithLocationAndStorage_ExistingLocationOrStorage()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        var entity = await dbContext.Images.AddTestAsset(assetId);
        var existingAsset = entity.Entity;
        await dbContext.ImageLocations.AddTestImageLocation(assetId);
        await dbContext.ImageStorages.AddTestImageStorage(assetId);
        await dbContext.CustomerStorages.AddTestCustomerStorage(sizeOfStored: 500, sizeOfThumbs: 800);
        await dbContext.SaveChangesAsync();

        var imageLocation = new ImageLocation { Id = assetId, S3 = "union-card", Nas = "wedding-coat" };
        var imageStorage = new ImageStorage
        {
            Id = assetId, Customer = 99, Space = 1, Size = 1010, CheckingInProgress = false,
            LastChecked = DateTime.UtcNow, ThumbnailSize = 2020
        };

        // Act
        var success = await sut.UpdateIngestedDeliverable(existingAsset, imageLocation, imageStorage, true);

        // Assert
        success.Should().BeTrue();

        var dbImageLocation = await dbContext.ImageLocations.SingleAsync(a => a.Id == assetId);
        dbImageLocation.Should().BeEquivalentTo(imageLocation);

        var dbImageStorage = await dbContext.ImageStorages.SingleAsync(a => a.Id == assetId);
        dbImageStorage.Should().BeEquivalentTo(imageStorage, opts => opts.Excluding(s => s.LastChecked));
        dbImageStorage.LastChecked.Should().BeCloseTo(imageStorage.LastChecked, TimeSpan.FromMinutes(1));

        var dbCustomerStorage = await dbContext.CustomerStorages.SingleAsync(cs => cs.Customer == 99 && cs.Space == 0);
        dbCustomerStorage.TotalSizeOfStoredImages.Should().Be(1510);
        dbCustomerStorage.TotalSizeOfThumbnails.Should().Be(2820);
        A.CallTo(() =>
                batchCompletedNotificationSender.SendBatchCompletedMessage(
                    A<Batch>._,
                    A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task UpdateIngestedAsset_UpdatesBatch_IfError()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        var waiting = AssetIdGenerator.GetAssetId(assetPostfix: "waiting");
        var failing = AssetIdGenerator.GetAssetId(assetPostfix: "fail");
        var complete = AssetIdGenerator.GetAssetId(assetPostfix: "complete");

        const int batchId = -10;

        var batch = await dbContext.Batches.AddTestBatch(batchId, count: 4, errors: 1, completed: 1);
        batch.Entity
            .AddBatchAsset(assetId)
            .AddBatchAsset(waiting)
            .AddBatchAsset(failing, BatchStatus.Error)
            .AddBatchAsset(complete, BatchStatus.Completed);
        await dbContext.Images.AddTestAsset(assetId, batch: batchId);
        await dbContext.Images.AddTestAsset(waiting, batch: batchId);
        await dbContext.Images.AddTestAsset(failing, batch: batchId);
        await dbContext.Images.AddTestAsset(complete, batch: batchId);
        await dbContext.SaveChangesAsync();

        var batchAsset = batch.Entity.BatchAssets.Single(b => b.AssetId == assetId);
        var newAsset = new Asset
        {
            Id = assetId, Reference1 = "bar", Ingesting = true, Width = 999, Height = 1000,
            Duration = 99, Batch = batchId, Customer = 99, Space = 1, Created = new DateTime(2021, 1, 1),
            Error = "broken state", BatchAssets = new List<BatchAsset> { batchAsset },
        };
        contextForTests.Images.Attach(newAsset);
        contextForTests.BatchAssets.Attach(batchAsset);

        // Act
        var success = await sut.UpdateIngestedDeliverable(newAsset, null, null, true);

        // Assert
        success.Should().BeTrue();

        var updatedItem = await dbContext.Batches
            .Include(b => b.BatchAssets)
            .SingleAsync(b => b.Id == batchId);
        updatedItem.Errors.Should().Be(2);
        updatedItem.Completed.Should().Be(1);
        updatedItem.Finished.Should().BeNull();

        updatedItem.BatchAssets.Single(ba => ba.AssetId == assetId).Status.Should().Be(BatchStatus.Error);
        A.CallTo(() =>
                batchCompletedNotificationSender.SendBatchCompletedMessage(
                    A<Batch>._,
                    A<CancellationToken>._))
            .MustNotHaveHappened();
    }
    
    [Fact]
    public async Task UpdateIngestedAsset_UpdatesBatch_IfComplete()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        var waiting = AssetIdGenerator.GetAssetId(assetPostfix: "waiting");
        var failing = AssetIdGenerator.GetAssetId(assetPostfix: "fail");
        var complete = AssetIdGenerator.GetAssetId(assetPostfix: "complete");

        const int batchId = -22;
        var batch = await dbContext.Batches.AddTestBatch(batchId, count: 4, errors: 1, completed: 1);
        batch.Entity
            .AddBatchAsset(assetId)
            .AddBatchAsset(waiting)
            .AddBatchAsset(failing, BatchStatus.Error)
            .AddBatchAsset(complete, BatchStatus.Completed);

        var entity = await dbContext.Images.AddTestAsset(assetId, batch: batchId);
        await dbContext.Images.AddTestAsset(waiting, batch: batchId);
        await dbContext.Images.AddTestAsset(failing, batch: batchId);
        await dbContext.Images.AddTestAsset(complete, batch: batchId);
        var existingAsset = entity.Entity;
        await dbContext.SaveChangesAsync();

        existingAsset.Width = 999;
        existingAsset.Height = 1000;
        existingAsset.Duration = 99;

        contextForTests.Images.Attach(existingAsset);

        // Act
        var success = await sut.UpdateIngestedDeliverable(existingAsset, null, null, true);

        // Assert
        success.Should().BeTrue();

        var updatedItem = await dbContext.Batches
            .Include(b => b.BatchAssets)
            .SingleAsync(b => b.Id == batchId);
        updatedItem.Errors.Should().Be(1);
        updatedItem.Completed.Should().Be(2);
        updatedItem.Finished.Should().BeNull();

        updatedItem.BatchAssets.Single(ba => ba.AssetId == assetId).Status.Should().Be(BatchStatus.Completed);
        A.CallTo(() =>
                batchCompletedNotificationSender.SendBatchCompletedMessage(
                    A<Batch>._,
                    A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task UpdateIngestedAsset_DoesNotUpdateBatch_IfIngestNotFinished()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        var waiting = AssetIdGenerator.GetAssetId(assetPostfix: "waiting");
        var failing = AssetIdGenerator.GetAssetId(assetPostfix: "fail");
        const int batchId = -111;
        var batch = await dbContext.Batches.AddTestBatch(batchId, count: 3, errors: 1, completed: 0);
        batch.Entity
            .AddBatchAsset(assetId)
            .AddBatchAsset(waiting)
            .AddBatchAsset(failing, BatchStatus.Error);
        var entity = await dbContext.Images.AddTestAsset(assetId, batch: batchId);
        await dbContext.Images.AddTestAsset(waiting, batch: batchId);
        await dbContext.Images.AddTestAsset(failing, batch: batchId);
        var existingAsset = entity.Entity;
        await dbContext.SaveChangesAsync();

        contextForTests.Images.Attach(existingAsset);

        existingAsset.Width = 999;
        existingAsset.Height = 1000;
        existingAsset.Duration = 99;
        existingAsset.Ingesting = true;

        // Act
        var success = await sut.UpdateIngestedDeliverable(existingAsset, null, null, false);

        // Assert
        success.Should().BeTrue();

        var updatedBatch = await dbContext.Batches
            .Include(b => b.BatchAssets)
            .SingleAsync(b => b.Id == batchId);
        updatedBatch.Errors.Should().Be(1);
        updatedBatch.Completed.Should().Be(0);
        updatedBatch.Finished.Should().BeNull();
        updatedBatch.BatchAssets.Single(ba => ba.AssetId == assetId).Status.Should().Be(BatchStatus.Waiting);

        var updatedImage = await dbContext.Images.SingleAsync(i => i.Id == assetId);
        updatedImage.Finished.Should().BeNull();
        updatedImage.Ingesting.Should().BeTrue();
        A.CallTo(() =>
                batchCompletedNotificationSender.SendBatchCompletedMessage(
                    A<Batch>._,
                    A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Theory]
    [InlineData(-12, "" )]
    [InlineData(-13, "error")]
    public async Task UpdateIngestedAsset_MarksBatchAsComplete_IfCompletedAndError_EqualsCount(int batchId, string error)
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId(assetPostfix: batchId.ToString());
        var failing = AssetIdGenerator.GetAssetId(assetPostfix: $"{batchId}fail");
        var complete = AssetIdGenerator.GetAssetId(assetPostfix: $"{batchId}complete");
        
        var batch = await dbContext.Batches.AddTestBatch(batchId, count: 3, errors: 1, completed: 1);
        batch.Entity
            .AddBatchAsset(assetId)
            .AddBatchAsset(failing, BatchStatus.Error)
            .AddBatchAsset(complete, BatchStatus.Completed);
        
        var entity = await dbContext.Images.AddTestAsset(assetId, batch: batchId);
        await dbContext.Images.AddTestAsset(failing, batch: batchId);
        await dbContext.Images.AddTestAsset(complete, batch: batchId);
        
        var existingAsset = entity.Entity;
        await dbContext.SaveChangesAsync();

        contextForTests.Images.Attach(existingAsset);

        existingAsset.Width = 999;
        existingAsset.Height = 1000;
        existingAsset.Duration = 99;
        existingAsset.Ingesting = true;
        existingAsset.Error = error;

        // Act
        var success = await sut.UpdateIngestedDeliverable(existingAsset, null, null, true);

        // Assert
        success.Should().BeTrue();

        var updatedItem = await dbContext.Batches.SingleAsync(b => b.Id == batchId);
        updatedItem.Finished.Should().NotBeNull();
        A.CallTo(() =>
                batchCompletedNotificationSender.SendBatchCompletedMessage(
                    A<Batch>.That.Matches(b => b.Id == batchId),
                    A<CancellationToken>._))
            .MustHaveHappened(1, Times.Exactly);
    }
    
    [Theory]
    [InlineData(-22, "" )]
    [InlineData(-23, "error")]
    public async Task UpdateIngestedAsset_DoesNotRaiseBatchCompleted_IfBatchAlreadyComplete(int batchId, string error)
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId(assetPostfix: batchId.ToString());
        var failing = AssetIdGenerator.GetAssetId(assetPostfix: $"{batchId}fail");
        var complete = AssetIdGenerator.GetAssetId(assetPostfix: $"{batchId}complete");

        var batchFinishedDate = DateTime.UtcNow.AddDays(-10);
        var finishedBatch = await dbContext.Batches.AddTestBatch(batchId, count: 3, errors: 1, completed: 1,
            finished: batchFinishedDate);
        finishedBatch.Entity
            .AddBatchAsset(assetId)
            .AddBatchAsset(failing, BatchStatus.Error)
            .AddBatchAsset(complete, BatchStatus.Completed);

        var entity = await dbContext.Images.AddTestAsset(assetId, batch: batchId);
        await dbContext.Images.AddTestAsset(failing, batch: batchId);
        await dbContext.Images.AddTestAsset(complete, batch: batchId);

        var existingAsset = entity.Entity;
        await dbContext.SaveChangesAsync();

        contextForTests.Images.Attach(existingAsset);
        existingAsset.Error = error;

        // Act
        var success = await sut.UpdateIngestedDeliverable(existingAsset, null, null, true);

        // Assert
        success.Should().BeTrue();

        var updatedBatch = await dbContext.Batches.SingleAsync(b => b.Id == batchId);
        updatedBatch.Finished.Should()
            .BeCloseTo(batchFinishedDate, TimeSpan.FromSeconds(2), "Finished date is not modified");
        A.CallTo(() =>
                batchCompletedNotificationSender.SendBatchCompletedMessage(
                    A<Batch>.That.Matches(b => b.Id == batchId),
                    A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task GetAdjunct_Null_IfNotFound()
    {
        var assetId = AssetIdGenerator.GetAssetId();
        var adjunct = await sut.GetAdjunct("nonexistent", assetId);
        adjunct.Should().BeNull();
    }

    [Fact]
    public async Task GetAdjunct_ReturnsAdjunct_WithoutBatchAdjuncts_IfNoBatchIdProvided()
    {
        var assetId = AssetIdGenerator.GetAssetId();
        const int batchId = -300;
        const string adjunctId = "adj-nobatch";
        var adjunctBatch = await dbContext.AdjunctBatches.AddTestAdjunctBatch(batchId);
        adjunctBatch.Entity.AddAdjunctBatchAdjunct(adjunctId, assetId);
        await dbContext.Images.AddTestAsset(assetId).WithTestAdjunct(adjunctId);
        await dbContext.SaveChangesAsync();

        var adjunct = await sut.GetAdjunct(adjunctId, assetId);

        adjunct.Should().NotBeNull();
        adjunct.AdjunctBatchAdjuncts.Should().BeNullOrEmpty("No batch Id specified");
    }

    [Fact]
    public async Task GetAdjunct_ReturnsAdjunct_WithMatchingBatchAdjunct_IfBatchIdSpecified()
    {
        var assetId = AssetIdGenerator.GetAssetId();
        const int batchId = -301;
        const int otherBatchId = -302;
        const string adjunctId = "adj-withbatch";
        var adjunctBatch = await dbContext.AdjunctBatches.AddTestAdjunctBatch(batchId);
        var otherAdjunctBatch = await dbContext.AdjunctBatches.AddTestAdjunctBatch(otherBatchId);
        adjunctBatch.Entity.AddAdjunctBatchAdjunct(adjunctId, assetId);
        otherAdjunctBatch.Entity.AddAdjunctBatchAdjunct(adjunctId, assetId);
        await dbContext.Images.AddTestAsset(assetId).WithTestAdjunct(adjunctId);
        await dbContext.SaveChangesAsync();

        var adjunct = await sut.GetAdjunct(adjunctId, assetId, batchId);

        adjunct.Should().NotBeNull();
        adjunct.AdjunctBatchAdjuncts.Should().ContainSingle(ba => ba.BatchId == batchId,
            "Only the specified batch record is returned");
    }

    [Fact]
    public async Task GetAdjunct_ReturnsAdjunct_WithEmptyBatchAdjuncts_IfBatchIdSpecified_ButNoRecordFound()
    {
        var assetId = AssetIdGenerator.GetAssetId();
        const string adjunctId = "adj-batchnotfound";
        await dbContext.Images.AddTestAsset(assetId).WithTestAdjunct(adjunctId);
        await dbContext.SaveChangesAsync();

        var adjunct = await sut.GetAdjunct(adjunctId, assetId, batchId: -999);

        adjunct.Should().NotBeNull();
        adjunct.AdjunctBatchAdjuncts.Should().BeNullOrEmpty("Batch Id specified but no matching record exists");
    }

    [Fact]
    public async Task UpdateIngestedAdjunct_UpdatesAdjunctBatch_IfError()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        const int batchId = -310;
        const string adjunctId = "adj-err";
        const string waitingAdjunctId = "adj-err-wait";

        var adjunctBatch = await dbContext.AdjunctBatches.AddTestAdjunctBatch(batchId, count: 2, errors: 0, completed: 0);
        adjunctBatch.Entity
            .AddAdjunctBatchAdjunct(adjunctId, assetId)
            .AddAdjunctBatchAdjunct(waitingAdjunctId, assetId);
        await dbContext.Images.AddTestAsset(assetId).WithTestAdjunct(adjunctId).WithTestAdjunct(waitingAdjunctId);
        await dbContext.SaveChangesAsync();

        var adjunct = await sut.GetAdjunct(adjunctId, assetId, batchId);
        adjunct.Error = "something went wrong";

        // Act
        var success = await sut.UpdateIngestedDeliverable(adjunct, null, null, true);

        // Assert
        success.Should().BeTrue();

        var updatedBatch = await dbContext.AdjunctBatches
            .Include(b => b.BatchAdjuncts)
            .SingleAsync(b => b.Id == batchId);
        updatedBatch.Errors.Should().Be(1);
        updatedBatch.Completed.Should().Be(0);
        updatedBatch.Finished.Should().BeNull();
        updatedBatch.BatchAdjuncts.Single(ba => ba.AdjunctId == adjunctId).Status.Should().Be(BatchStatus.Error);
    }

    [Fact]
    public async Task UpdateIngestedAdjunct_UpdatesAdjunctBatch_IfComplete()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        const int batchId = -311;
        const string adjunctId = "adj-comp";
        const string waitingAdjunctId = "adj-comp-wait";

        var adjunctBatch = await dbContext.AdjunctBatches.AddTestAdjunctBatch(batchId, count: 2, errors: 0, completed: 0);
        adjunctBatch.Entity
            .AddAdjunctBatchAdjunct(adjunctId, assetId)
            .AddAdjunctBatchAdjunct(waitingAdjunctId, assetId);
        await dbContext.Images.AddTestAsset(assetId).WithTestAdjunct(adjunctId).WithTestAdjunct(waitingAdjunctId);
        await dbContext.SaveChangesAsync();

        var adjunct = await sut.GetAdjunct(adjunctId, assetId, batchId);

        // Act
        var success = await sut.UpdateIngestedDeliverable(adjunct, null, null, true);

        // Assert
        success.Should().BeTrue();

        var updatedBatch = await dbContext.AdjunctBatches
            .Include(b => b.BatchAdjuncts)
            .SingleAsync(b => b.Id == batchId);
        updatedBatch.Errors.Should().Be(0);
        updatedBatch.Completed.Should().Be(1);
        updatedBatch.Finished.Should().BeNull();
        updatedBatch.BatchAdjuncts.Single(ba => ba.AdjunctId == adjunctId).Status.Should().Be(BatchStatus.Completed);
    }

    [Fact]
    public async Task UpdateIngestedAdjunct_DoesNotUpdateAdjunctBatch_IfIngestNotFinished()
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId();
        const int batchId = -312;
        const string adjunctId = "adj-notfinished";
        const string waitingAdjunctId = "adj-notfinished-wait";

        var adjunctBatch = await dbContext.AdjunctBatches.AddTestAdjunctBatch(batchId, count: 2, errors: 0, completed: 0);
        adjunctBatch.Entity
            .AddAdjunctBatchAdjunct(adjunctId, assetId)
            .AddAdjunctBatchAdjunct(waitingAdjunctId, assetId);
        await dbContext.Images.AddTestAsset(assetId).WithTestAdjunct(adjunctId).WithTestAdjunct(waitingAdjunctId);
        await dbContext.SaveChangesAsync();

        var adjunct = await sut.GetAdjunct(adjunctId, assetId, batchId);
        adjunct.Ingesting = true;

        // Act
        var success = await sut.UpdateIngestedDeliverable(adjunct, null, null, false);

        // Assert
        success.Should().BeTrue();

        var updatedBatch = await dbContext.AdjunctBatches
            .Include(b => b.BatchAdjuncts)
            .SingleAsync(b => b.Id == batchId);
        updatedBatch.Errors.Should().Be(0);
        updatedBatch.Completed.Should().Be(0);
        updatedBatch.Finished.Should().BeNull();
        updatedBatch.BatchAdjuncts.Single(ba => ba.AdjunctId == adjunctId).Status.Should().Be(BatchStatus.Waiting);

        var updatedAdjunct = await dbContext.Adjuncts.SingleAsync(a => a.Id == adjunctId && a.AssetId == assetId);
        updatedAdjunct.Finished.Should().BeNull();
        updatedAdjunct.Ingesting.Should().BeTrue();
    }

    [Theory]
    [InlineData(-320, "")]
    [InlineData(-321, "error")]
    public async Task UpdateIngestedAdjunct_MarksAdjunctBatchAsComplete_IfCompletedAndError_EqualsCount(int batchId, string error)
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId(assetPostfix: batchId.ToString());
        var adjunctId = $"adj-{batchId}";
        var failingAdjunctId = $"adj-{batchId}-fail";
        var completedAdjunctId = $"adj-{batchId}-comp";

        var adjunctBatch = await dbContext.AdjunctBatches.AddTestAdjunctBatch(batchId, count: 3, errors: 1, completed: 1);
        adjunctBatch.Entity
            .AddAdjunctBatchAdjunct(adjunctId, assetId)
            .AddAdjunctBatchAdjunct(failingAdjunctId, assetId, BatchStatus.Error)
            .AddAdjunctBatchAdjunct(completedAdjunctId, assetId, BatchStatus.Completed);
        await dbContext.Images.AddTestAsset(assetId)
            .WithTestAdjunct(adjunctId)
            .WithTestAdjunct(failingAdjunctId)
            .WithTestAdjunct(completedAdjunctId);
        await dbContext.SaveChangesAsync();

        var adjunct = await sut.GetAdjunct(adjunctId, assetId, batchId);
        adjunct.Error = error;

        // Act
        var success = await sut.UpdateIngestedDeliverable(adjunct, null, null, true);

        // Assert
        success.Should().BeTrue();

        var updatedBatch = await dbContext.AdjunctBatches.SingleAsync(b => b.Id == batchId);
        updatedBatch.Finished.Should().NotBeNull();
    }

    [Theory]
    [InlineData(-330, "")]
    [InlineData(-331, "error")]
    public async Task UpdateIngestedAdjunct_DoesNotFinishAdjunctBatch_IfAlreadyComplete(int batchId, string error)
    {
        // Arrange
        var assetId = AssetIdGenerator.GetAssetId(assetPostfix: batchId.ToString());
        var adjunctId = $"adj-{batchId}";
        var failingAdjunctId = $"adj-{batchId}-fail";
        var completedAdjunctId = $"adj-{batchId}-comp";

        var batchFinishedDate = DateTime.UtcNow.AddDays(-10);
        var adjunctBatch = await dbContext.AdjunctBatches.AddTestAdjunctBatch(batchId, count: 3, errors: 1, completed: 1,
            finished: batchFinishedDate);
        adjunctBatch.Entity
            .AddAdjunctBatchAdjunct(adjunctId, assetId)
            .AddAdjunctBatchAdjunct(failingAdjunctId, assetId, BatchStatus.Error)
            .AddAdjunctBatchAdjunct(completedAdjunctId, assetId, BatchStatus.Completed);
        await dbContext.Images.AddTestAsset(assetId)
            .WithTestAdjunct(adjunctId)
            .WithTestAdjunct(failingAdjunctId)
            .WithTestAdjunct(completedAdjunctId);
        await dbContext.SaveChangesAsync();

        var adjunct = await sut.GetAdjunct(adjunctId, assetId, batchId);
        adjunct.Error = error;

        // Act
        var success = await sut.UpdateIngestedDeliverable(adjunct, null, null, true);

        // Assert
        success.Should().BeTrue();

        var updatedBatch = await dbContext.AdjunctBatches.SingleAsync(b => b.Id == batchId);
        updatedBatch.Finished.Should()
            .BeCloseTo(batchFinishedDate, TimeSpan.FromSeconds(2), "Finished date is not modified");
    }
}
