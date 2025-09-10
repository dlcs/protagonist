using Amazon.MediaConvert;
using Amazon.MediaConvert.Model;
using DLCS.AWS.S3.Models;
using DLCS.AWS.Transcoding;
using DLCS.AWS.Transcoding.Models.Job;
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
            CreatedAt = job.CreatedAt,
            Status = job.Status.ToString(),
            PipelineId = job.Queue.EverythingAfterLast('/'),
            Outputs = CreateOutputs(job, assetId),
            Input = CreateInput(job.Settings.Inputs.Single()),
            Timing = CreateTiming(job.Timing),
            UserMetadata = job.UserMetadata,
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

        // Timing has DateTime properties backed by DateTime?, the getters for these call .GetValueOrDefault()
        long? ToUnixTimeMilliseconds(DateTime time) =>
            time == DateTime.MinValue ? null : ((DateTimeOffset)time).ToUnixTimeMilliseconds();
    }

    private static List<TranscoderJob.TranscoderOutput> CreateOutputs(Job job, AssetId assetId)
    {
        /*
         There are 2 related properties: OutputGroupDetails and Settings.OutputGroups.
         The former contains values calculated during encoding: Duration, Width and Height.
         The latter contains values provided when creating job: preset, extension, name-modifier
         Both OutputGroupDetails and Settings.OutputGroups are collections but there'll only ever be 1 of each */
        
        var jobIsComplete = job.Status == JobStatus.COMPLETE;
        var outputGroupDetails = job.OutputGroupDetails.SingleOrDefault();
        
        // If there are not OutputGroupDetails then nothing was transcoded so abort
        if (outputGroupDetails == null) return []; 
        
        var mediaType = job.UserMetadata[TranscodeMetadataKeys.MediaType]!;
        var outputGroup = job.Settings.OutputGroups.Single();
        var destinationKey = GetDestinationKey(outputGroup.OutputGroupSettings.FileGroupSettings.Destination);

        var transcodeOutputs = new List<TranscoderJob.TranscoderOutput>(outputGroupDetails.OutputDetails.Count);

        for (var x = 0; x < outputGroupDetails.OutputDetails.Count; x++)
        {
            var output = outputGroup.Outputs[x]!;
            var outputDetail = outputGroupDetails.OutputDetails[x]!;

            var storageKeys = GetFinalStorageKeys(destinationKey, output, jobIsComplete, assetId, mediaType);

            var transcodeOutput = new TranscoderJob.TranscoderOutput
            {
                Id = x.ToString(),
                Duration = outputDetail.DurationInMs > 0 ? outputDetail.DurationInMs / 1000 : 0,
                DurationMillis = outputDetail.DurationInMs,
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
    /// Get "Key" part of the destination (s3://timebased-output/1234/2/1/foo/trancode => 1234/2/1/foo/trancode)
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
        // And calculate the key of output (1234/2/1/foo/trancode => 1234/2/1/foo/trancode_1.mp4)
        var outputKey = $"{destinationKey}{output.NameModifier}.{output.Extension}";

        if (!isComplete) return (outputKey, null);

        var storageKey = TranscoderTemplates.GetTranscodeKey(mediaType, assetId, output.Extension);
        return (outputKey, storageKey);
    }
}
