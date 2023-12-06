using System.Text;
using DLCS.AWS.ElasticTranscoder;
using DLCS.AWS.ElasticTranscoder.Models;
using DLCS.AWS.S3;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using Engine.Data;

namespace Engine.Ingest.Timebased.Completion;

public class TimebasedIngestorCompletion : ITimebasedIngestorCompletion
{
    private readonly IEngineAssetRepository assetRepository;
    private readonly IStorageKeyGenerator storageKeyGenerator;
    private readonly IBucketWriter bucketWriter;
    private readonly ILogger<TimebasedIngestorCompletion> logger;

    public TimebasedIngestorCompletion(
        IEngineAssetRepository assetRepository,
        IStorageKeyGenerator storageKeyGenerator,
        IBucketWriter bucketWriter,
        ILogger<TimebasedIngestorCompletion> logger)
    {
        this.assetRepository = assetRepository;
        this.storageKeyGenerator = storageKeyGenerator;
        this.bucketWriter = bucketWriter;
        this.logger = logger;
    }

    public async Task<bool> CompleteSuccessfulIngest(AssetId assetId, TranscodeResult transcodeResult,
        CancellationToken cancellationToken = default)
    {
        var asset = await assetRepository.GetAsset(assetId, cancellationToken);

        if (asset == null)
        {
            logger.LogError("Unable to find asset {AssetId} in database", assetId);
            return false;
        }

        var errors = new List<string>();
        var transcodeSuccess = true;
        
        if (!transcodeResult.IsComplete())
        {
            transcodeSuccess = false;
            errors.Add(
                $"Transcode failed with status: {transcodeResult.State}. Error: {transcodeResult.ErrorCode ?? "unknown"}");
        }

        var copyTasks = CopyTranscodeOutputs(transcodeResult, errors, asset, cancellationToken);

        await DeleteInputFile(transcodeResult);
        
        var copyResults = await Task.WhenAll(copyTasks);
        var size = GetAssetStorageSize(transcodeResult, copyResults);

        foreach (var cr in copyResults)
        {
            if (!transcodeSuccess) break;
            
            /*
             * A copy result is deemed as okay if:
             *  The result is 'Success', OR
             *  The source file was 'NotFound' and the Destination already exists. This likely happened due to the
             *  message being a retry
             */ 
            if (cr.Result == LargeObjectStatus.Success) continue;
            if (cr is { Result: LargeObjectStatus.SourceNotFound, DestinationExists: true }) continue;
            
            errors.Add($"Copying ElasticTranscoder output failed with reason: {cr.Result}");
            transcodeSuccess = false;
        }
        
        if (errors.Count > 0)
        {
            asset.Error = string.Join("|", errors);
        }
        
        var dbUpdateSuccess = await CompleteAssetInDatabase(asset, size, cancellationToken);
        
        return transcodeSuccess && dbUpdateSuccess;
    }

    private List<Task<LargeObjectCopyResult>> CopyTranscodeOutputs(TranscodeResult transcodeResult,
        List<string> errors, Asset asset, CancellationToken cancellationToken)
    {
        bool dimensionsUpdated = false;
        var transcodeOutputs = transcodeResult.Outputs;
        var copyTasks = new List<Task<LargeObjectCopyResult>>(transcodeOutputs.Count);

        foreach (var transcodeOutput in transcodeOutputs)
        {
            if (!transcodeOutput.IsComplete())
            {
                logger.LogWarning("Received incomplete {Status} for ElasticTranscoder output for {OutputKey}",
                    transcodeOutput.Status, transcodeOutput.Key);
                errors.Add(
                    $"Transcode output for {transcodeOutput.Key} has status {transcodeOutput.Status} with detail {transcodeOutput.StatusDetail}");
                continue;
            }

            SetAssetDimensions(asset, dimensionsUpdated, transcodeOutput);
            dimensionsUpdated = true;

            // Move assets from elastic transcoder-output bucket to main bucket
            copyTasks.Add(CopyTranscodeOutputToStorage(transcodeOutput, asset.Id, cancellationToken));
        }

        return copyTasks;
    }

    private static long GetAssetStorageSize(TranscodeResult transcodeResult, LargeObjectCopyResult[] copyResults)
    {
        var derivativeSizes = copyResults.Sum(result => result.Size ?? 0);
        var totalSize = derivativeSizes + transcodeResult.GetStoredOriginalAssetSize();
        return totalSize;
    }

    private async Task<bool> CompleteAssetInDatabase(Asset asset, long? assetSize = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ImageStorage? imageStore = null;
            ImageLocation? imageLocation = null;
            if (assetSize.HasValue)
            {
                imageStore = new ImageStorage
                {
                    Id = asset.Id,
                    Customer = asset.Customer,
                    Space = asset.Space,
                    LastChecked = DateTime.UtcNow,
                    Size = assetSize.Value
                };

                // NOTE - ImageLocation isn't used for 'T', only 'I' family so just set an empty record
                imageLocation = new ImageLocation { Id = asset.Id, Nas = string.Empty, S3 = string.Empty };
            }

            var success =
                await assetRepository.UpdateIngestedAsset(asset, imageLocation, imageStore, true, cancellationToken);
            return success;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error marking AV asset as completed '{AssetId}'", asset.Id);
            return false;
        }
    }
    
    private void SetAssetDimensions(Asset asset, bool dimensionsUpdated, TranscodeOutput transcodeOutput)
    {
        var transcodeDuration = transcodeOutput.GetDuration();
        if (!dimensionsUpdated)
        {
            asset.Width = transcodeOutput.Width;
            asset.Height = transcodeOutput.Height;
            asset.Duration = transcodeDuration;
        }
        else if (transcodeDuration != asset.Duration)
        {
            // There may be a very slight difference in outputs
            logger.LogWarning("Asset {AssetId} has outputs with different durations: {Duration1}ms and {Duration2}ms",
                asset.Id, asset.Duration, transcodeDuration);
        }
    }

    private async Task<LargeObjectCopyResult> CopyTranscodeOutputToStorage(TranscodeOutput transcodeOutput,
        AssetId assetId, CancellationToken cancellationToken)
    {
        var source = storageKeyGenerator.GetTimebasedOutputLocation(transcodeOutput.Key);
        var destination =
            storageKeyGenerator.GetTimebasedAssetLocation(
                TranscoderTemplates.GetFinalDestinationKey(transcodeOutput.Key));

        var copyResult =
            await bucketWriter.CopyLargeObject(source, destination, token: cancellationToken);

        if (copyResult.Result == LargeObjectStatus.Success)
        {
            // delete output file for ElasticTranscoder
            await bucketWriter.DeleteFromBucket(source);
            logger.LogDebug("Successfully copied transcoder output {OutputKey} to storage {StorageKey} for {AssetId}",
                source, destination, assetId);
        }
        else if (copyResult.Result == LargeObjectStatus.SourceNotFound)
        {
            logger.LogInformation(
                "Unable to find completed transcoded output {OutputKey} for {AssetId}, destination exists: {DestinationExists}",
                source, assetId, copyResult.DestinationExists);
        }

        return copyResult;
    }

    private async Task DeleteInputFile(TranscodeResult transcodeResult)
    {
        if (string.IsNullOrEmpty(transcodeResult.InputKey)) return;

        var inputKey = storageKeyGenerator.GetTimebasedInputLocation(transcodeResult.InputKey);
        await bucketWriter.DeleteFromBucket(inputKey);
    }
}