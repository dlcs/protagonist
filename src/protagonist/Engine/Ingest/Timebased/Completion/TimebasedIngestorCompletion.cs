using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.AWS.Transcoding;
using DLCS.AWS.Transcoding.Models.Job;
using DLCS.Core;
using DLCS.Core.Collections;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Assets.Metadata;
using Engine.Data;

namespace Engine.Ingest.Timebased.Completion;

public class TimebasedIngestorCompletion(
    IEngineAssetRepository assetRepository,
    IStorageKeyGenerator storageKeyGenerator,
    IBucketWriter bucketWriter,
    ITranscoderWrapper transcoderWrapper,
    ILogger<TimebasedIngestorCompletion> logger)
    : ITimebasedIngestorCompletion
{
    public async Task<bool> CompleteSuccessfulIngest(AssetId assetId, int? batchId, string jobId,
        CancellationToken cancellationToken = default)
    {
        var asset = await assetRepository.GetAsset(assetId, batchId, cancellationToken);

        if (asset == null)
        {
            logger.LogError("Unable to find asset {AssetId} in database", assetId);
            return false;
        }

        var transcodeJob = await transcoderWrapper.GetTranscoderJob(assetId, jobId, cancellationToken);
        if (transcodeJob == null)
        {
            logger.LogError("Unable to find transcoding job {JobId}", jobId);
            return false;
        }

        var errors = new List<string>();
        var transcodeSuccess = true;
        
        if (!transcodeJob.IsComplete())
        {
            transcodeSuccess = false;
            var errorCode = transcodeJob.ErrorCode.HasValue ? transcodeJob.ErrorCode.ToString() : "unknown";
            errors.Add($"Transcode failed with status: {transcodeJob.ErrorMessage}. Error: {errorCode}");
        }

        var size = await CopyTranscodeOutputs(transcodeJob, errors, asset, cancellationToken);
        await DeleteInputFile(transcodeJob);
        
        if (errors.Count > 0)
        {
            transcodeSuccess = false;
            asset.Error = string.Join("|", errors);
        }
        
        var dbUpdateSuccess = await CompleteAssetInDatabase(asset, size, cancellationToken);
        return transcodeSuccess && dbUpdateSuccess;
    }

    private async Task<long> CopyTranscodeOutputs(TranscoderJob transcodeJob, List<string> errors, Asset asset, 
        CancellationToken cancellationToken)
    {
        // We may be storing the original AV file - if so use that size as starting count. 
        var transcodeSizeRunningTotal = transcodeJob.GetStoredOriginalAssetSize();
        
        if (!transcodeJob.IsComplete()) return transcodeSizeRunningTotal;
        
        bool dimensionsUpdated = false;
        var transcodeOutputs = transcodeJob.Outputs;
        var applicationMetadata = new List<AVTranscode>(transcodeOutputs.Count);

        foreach (var transcodeOutput in transcodeOutputs)
        {
            // Get the destination where we will store the transcode
            var outputDestination = GetFinalDestinationForOutput(transcodeOutput);

            SetAssetDimensions(asset, dimensionsUpdated, transcodeOutput);
            dimensionsUpdated = true;

            // Move assets from elastic transcoder-output bucket to main bucket
            var copyResult =
                await CopyTranscodeOutputToStorage(transcodeOutput, outputDestination, asset.Id, cancellationToken);
            if (IsCopySuccessful(copyResult))
            {
                var avTranscode = MakeAvTranscode(transcodeOutput, outputDestination, asset);
                applicationMetadata.Add(avTranscode);

                transcodeSizeRunningTotal += copyResult.Size ?? 0;
            }
            else
            {
                errors.Add($"Copying transcode output failed with reason: {copyResult.Result}");
            }
        }

        if (!applicationMetadata.IsNullOrEmpty())
        {
            asset.UpsertApplicationMetadata(AssetApplicationMetadataTypes.AVTranscodes, applicationMetadata.ToArray());
        }
        
        return transcodeSizeRunningTotal;
    }
    
    private static bool IsCopySuccessful(LargeObjectCopyResult copyResult)
    {
        /*
         * A copy result is deemed as success if:
         *  The result is 'Success', OR
         *  The source file was 'NotFound' and the Destination already exists. This likely happened due to the
         *  message being a retry.
         */ 
        return copyResult.Result == LargeObjectStatus.Success || copyResult is
            { Result: LargeObjectStatus.SourceNotFound, DestinationExists: true };
    }

    private static AVTranscode MakeAvTranscode(TranscoderJob.TranscoderOutput transcodeOutput,
        ObjectInBucket outputDestination, Asset asset)
    {
        var extension = transcodeOutput.Extension;
        var assetIsVideo = MIMEHelper.IsVideo(asset.MediaType);

        var avTranscode = new AVTranscode
        {
            Duration = transcodeOutput.DurationMillis,
            Location = outputDestination.GetS3Uri(),
            Extension = extension,
            TranscodeName = transcodeOutput.PresetId,
            MediaType = MIMEHelper.GetContentTypeForExtension(extension) ??
                        (assetIsVideo ? $"video/{extension}" : $"audio/{extension}"),
        };

        if (assetIsVideo)
        {
            avTranscode.Height = transcodeOutput.Height;
            avTranscode.Width = transcodeOutput.Width;
        }

        return avTranscode;
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
    
    private void SetAssetDimensions(Asset asset, bool dimensionsUpdated, TranscoderJob.TranscoderOutput transcodeOutput)
    {
        var transcodeDuration = transcodeOutput.DurationMillis;
        if (!dimensionsUpdated)
        {
            asset.Width = transcodeOutput.Width ?? 0;
            asset.Height = transcodeOutput.Height ?? 0;
            asset.Duration = transcodeDuration;
        }
        else if (transcodeDuration != asset.Duration)
        {
            // There may be a very slight difference in outputs
            logger.LogWarning("Asset {AssetId} has outputs with different durations: {Duration1}ms and {Duration2}ms",
                asset.Id, asset.Duration, transcodeDuration);
        }
    }

    private ObjectInBucket GetFinalDestinationForOutput(TranscoderJob.TranscoderOutput transcodeOutput)
        => storageKeyGenerator.GetTimebasedAssetLocation(transcodeOutput.Key!);

    private async Task<LargeObjectCopyResult> CopyTranscodeOutputToStorage(TranscoderJob.TranscoderOutput transcodeOutput,
        ObjectInBucket destination, AssetId assetId, CancellationToken cancellationToken)
    {
        var source = storageKeyGenerator.GetTimebasedOutputLocation(transcodeOutput.TranscodeKey);
        var copyResult = await bucketWriter.CopyLargeObject(source, destination, token: cancellationToken);

        if (copyResult.Result == LargeObjectStatus.Success)
        {
            // Delete output file for MediaConvert
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

    private async Task DeleteInputFile(TranscoderJob transcodeOutput)
    {
        if (string.IsNullOrEmpty(transcodeOutput.Input.Input)) return;

        var inputKey = storageKeyGenerator.GetTimebasedInputLocation(transcodeOutput.Input.Input);
        await bucketWriter.DeleteFromBucket(inputKey);
    }
}
