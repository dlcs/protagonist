using DLCS.AWS.ElasticTranscoder;
using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.AWS.Transcoding;
using DLCS.AWS.Transcoding.Models;
using DLCS.Core;
using DLCS.Core.Collections;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Assets.Metadata;
using Engine.Data;

namespace Engine.Ingest.Timebased.Completion;

public class TimebasedIngestorCompletion : ITimebasedIngestorCompletion
{
    private readonly IEngineAssetRepository assetRepository;
    private readonly IStorageKeyGenerator storageKeyGenerator;
    private readonly IBucketWriter bucketWriter;
    private readonly IElasticTranscoderPresetLookup elasticTranscoderPresetLookup;
    private readonly ILogger<TimebasedIngestorCompletion> logger;

    public TimebasedIngestorCompletion(
        IEngineAssetRepository assetRepository,
        IStorageKeyGenerator storageKeyGenerator,
        IBucketWriter bucketWriter,
        IElasticTranscoderPresetLookup elasticTranscoderPresetLookup,
        ILogger<TimebasedIngestorCompletion> logger)
    {
        this.assetRepository = assetRepository;
        this.storageKeyGenerator = storageKeyGenerator;
        this.bucketWriter = bucketWriter;
        this.elasticTranscoderPresetLookup = elasticTranscoderPresetLookup;
        this.logger = logger;
    }

    public async Task<bool> CompleteSuccessfulIngest(AssetId assetId, int? batchId, TranscodeResult transcodeResult,
        CancellationToken cancellationToken = default)
    {
        var asset = await assetRepository.GetAsset(assetId, batchId, cancellationToken);

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
            var errorCode = transcodeResult.ErrorCode.HasValue ? transcodeResult.ErrorCode.ToString() : "unknown";
            errors.Add($"Transcode failed with status: {transcodeResult.State}. Error: {errorCode}");
        }

        var size = await CopyTranscodeOutputs(transcodeResult, errors, asset, cancellationToken);
        await DeleteInputFile(transcodeResult);
        
        if (errors.Count > 0)
        {
            transcodeSuccess = false;
            asset.Error = string.Join("|", errors);
        }
        
        var dbUpdateSuccess = await CompleteAssetInDatabase(asset, size, cancellationToken);
        return transcodeSuccess && dbUpdateSuccess;
    }

    private async Task<long> CopyTranscodeOutputs(TranscodeResult transcodeResult,
        List<string> errors, Asset asset, CancellationToken cancellationToken)
    {
        bool dimensionsUpdated = false;
        var transcodeOutputs = transcodeResult.Outputs;
        var applicationMetadata = new List<AVTranscode>(transcodeOutputs.Count);
        var transcodeSizeRunningTotal = 0L;

        var presetLookup = await elasticTranscoderPresetLookup.GetPresetLookupById(cancellationToken);
        
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

            if (!presetLookup.TryGetValue(transcodeOutput.PresetId, out var preset))
            {
                logger.LogError("Unable to find preset for {PresetId}", transcodeOutput.PresetId);
                continue;
            }

            // Get the destination where we will store the transcode, from that take the file extension as this will 
            // inform the mediatype
            var outputDestination = GetFinalDestinationForOutput(transcodeOutput);

            SetAssetDimensions(asset, dimensionsUpdated, transcodeOutput);
            dimensionsUpdated = true;

            // Move assets from elastic transcoder-output bucket to main bucket
            var copyResult =
                await CopyTranscodeOutputToStorage(transcodeOutput, outputDestination, asset.Id, cancellationToken);
            if (IsCopySuccessful(copyResult))
            {
                var avTranscode = MakeAvTranscode(transcodeOutput, outputDestination, asset, preset);
                applicationMetadata.Add(avTranscode);

                transcodeSizeRunningTotal += copyResult.Size ?? 0;
            }
            else
            {
                errors.Add($"Copying ElasticTranscoder output failed with reason: {copyResult.Result}");
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

    private static AVTranscode MakeAvTranscode(TranscodeOutput transcodeOutput, ObjectInBucket outputDestination,
        Asset asset, TranscoderPreset preset)
    {
        var extension = preset.Extension;
        var assetIsVideo = MIMEHelper.IsVideo(asset.MediaType);

        var avTranscode = new AVTranscode
        {
            Duration = transcodeOutput.GetDuration(),
            Location = outputDestination.GetS3Uri(),
            Extension = extension,
            TranscodeName = preset.Name,
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

    private ObjectInBucket GetFinalDestinationForOutput(TranscodeOutput transcodeOutput)
        => storageKeyGenerator.GetTimebasedAssetLocation(
            TranscoderTemplates.GetFinalDestinationKey(transcodeOutput.Key));

    private async Task<LargeObjectCopyResult> CopyTranscodeOutputToStorage(TranscodeOutput transcodeOutput,
        ObjectInBucket destination, AssetId assetId, CancellationToken cancellationToken)
    {
        var source = storageKeyGenerator.GetTimebasedOutputLocation(transcodeOutput.Key);
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
