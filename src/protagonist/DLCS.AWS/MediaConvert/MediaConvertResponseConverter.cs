using Amazon.MediaConvert;
using Amazon.MediaConvert.Model;
using DLCS.AWS.S3.Models;
using DLCS.AWS.Transcoding;
using DLCS.AWS.Transcoding.Models.Job;
using DLCS.Core.Collections;
using DLCS.Core.Strings;
using DLCS.Core.Types;

namespace DLCS.AWS.MediaConvert;

public static class MediaConvertResponseConverter
{
    /// <summary>
    /// Convert provided MediaConvert <see cref="Job"/> to internal <see cref="TranscoderJob"/> representation
    /// </summary>
    public static TranscoderJob CreateTranscoderJob(Job job, AssetId assetId) =>
        new()
        {
            Id = job.Id,
            CreatedAt = job.CreatedAt.GetValueOrDefault(),
            Status = job.Status.ToString(),
            PipelineId = job.Queue.EverythingAfterLast('/'),
            Outputs = CreateOutputs(job, assetId),
            Input = CreateInput(job.Settings.Inputs.Single()),
            Timing = CreateTiming(job.Timing),
            UserMetadata = job.UserMetadata ?? new Dictionary<string, string>(),
            ErrorCode = job.ErrorCode == 0 ? null : job.ErrorCode,
            ErrorMessage = job.ErrorMessage,
        };

    private static TranscoderJob.TranscoderInput CreateInput(Input jobInput) => new() { Input = jobInput.FileInput, };

    private static TranscoderJob.TranscoderTiming CreateTiming(Timing timing)
    {
        return new()
        {
            FinishTimeMillis = ToUnixTimeMilliseconds(timing.FinishTime),
            StartTimeMillis = ToUnixTimeMilliseconds(timing.StartTime),
            SubmitTimeMillis = ToUnixTimeMilliseconds(timing.SubmitTime) ?? 0,
        };

        // Timing exposes DateTime? properties, which are unset for stages the job hasn't reached
        long? ToUnixTimeMilliseconds(DateTime? time) =>
            time is null || time == DateTime.MinValue ? null : ((DateTimeOffset)time.Value).ToUnixTimeMilliseconds();
    }

    private static List<TranscoderJob.TranscoderOutput> CreateOutputs(Job job, AssetId assetId)
    {
        /*
         There are 2 related properties: OutputGroupDetails and Settings.OutputGroups.
         The former contains values calculated during encoding: Duration, Width and Height.
         The latter contains values provided when creating job: preset, extension, name-modifier
         Both OutputGroupDetails and Settings.OutputGroups are collections but there'll only ever be 1 of each */
        
        var jobIsComplete = job.Status == JobStatus.COMPLETE;
        var outputGroupDetails = job.OutputGroupDetails?.SingleOrDefault();
        
        // If there are not OutputGroupDetails then nothing was transcoded so abort
        if (outputGroupDetails == null) return []; 
        
        // AWSSDK v4 leaves response collections null, rather than empty, when the service returns no elements.
        // An errored job can return an OutputGroupDetails entry that has no OutputDetails at all
        var outputDetails = outputGroupDetails.OutputDetails;
        if (outputDetails.IsNullOrEmpty()) return [];

        // Read UserMetadata here rather than relying on the null-coalesce in CreateTranscoderJob - object initializer
        // members are evaluated in source order, so Outputs (and therefore this method) runs before it
        if (job.UserMetadata is not { } userMetadata ||
            !userMetadata.TryGetValue(TranscodeMetadataKeys.MediaType, out var mediaType))
        {
            throw new InvalidOperationException(
                $"MediaConvert job {job.Id} has no '{TranscodeMetadataKeys.MediaType}' user-metadata");
        }

        var outputGroup = job.Settings.OutputGroups.Single();
        var destinationKey = GetDestinationKey(outputGroup.OutputGroupSettings.FileGroupSettings.Destination);

        var transcodeOutputs = new List<TranscoderJob.TranscoderOutput>(outputDetails.Count);

        for (var x = 0; x < outputDetails.Count; x++)
        {
            var output = outputGroup.Outputs[x]!;
            var outputDetail = outputDetails[x]!;

            var storageKeys = GetFinalStorageKeys(destinationKey, output, jobIsComplete, assetId, mediaType);

            var transcodeOutput = new TranscoderJob.TranscoderOutput
            {
                Id = x.ToString(),
                Duration = outputDetail.DurationInMs > 0 ? outputDetail.DurationInMs.Value / 1000 : 0,
                DurationMillis = outputDetail.DurationInMs.GetValueOrDefault(),
                Height = outputDetail.VideoDetails?.HeightInPx,
                Width = outputDetail.VideoDetails?.WidthInPx,
                TranscodeKey = storageKeys.TranscodeKey,
                Key = storageKeys.DlcsKey,
                Extension = output.Extension,
                PresetId = output.Preset,
            };
            transcodeOutputs.Add(transcodeOutput);
        }

        return transcodeOutputs;
    }
    
    /// <summary>
    /// Get "Key" part of the destination (s3://timebased-output/1234/2/1/foo/transcode => 1234/2/1/foo/transcode)
    /// This serves as the prefix that will be used for all outputs
    /// </summary>
    private static string GetDestinationKey(string destination)
    {
        var destinationKey = RegionalisedObjectInBucket.Parse(destination, true)!.Key!;
        return destinationKey;
    }

    private static (string TranscodeKey, string? DlcsKey) GetFinalStorageKeys(string destinationKey, Output output,
        bool isComplete, AssetId assetId, string mediaType)
    {
        // And calculate the key of output (1234/2/1/foo/transcode => 1234/2/1/foo/transcode_1.mp4)
        var outputKey = $"{destinationKey}{output.NameModifier}.{output.Extension}";

        if (!isComplete) return (outputKey, null);

        var storageKey = TranscoderTemplates.GetTranscodeKey(mediaType, assetId, output.Extension);
        return (outputKey, storageKey);
    }
}
