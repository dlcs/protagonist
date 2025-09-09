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
    /// Convert provided MediaConver <see cref="Job"/> to <see cref="TranscoderJob"/>
    /// </summary>
    public static TranscoderJob CreateTranscoderJob(Job job, AssetId assetId)
    {
        // Note that output details are split between OutputGroupDetails and job.Settings.OutputGroups but these always
        // have the same number of items in the same order
        var jobSettings = job.Settings;

        var transcoderJob = new TranscoderJob
        {
            Id = job.Id,
            CreatedAt = job.CreatedAt,
            Status = job.Status.ToString(),
            PipelineId = job.Queue.EverythingAfterLast('/'),
            Outputs = CreateOutputs(job, assetId),
            Input = CreateInput(jobSettings.Inputs.Single()),
            Timing = CreateTiming(job.Timing),
            UserMetadata = job.UserMetadata,
            ErrorCode = job.ErrorCode == 0 ? null : job.ErrorCode,
            ErrorMessage = job.ErrorMessage,
        };

        return transcoderJob;
    }

    private static TranscoderJob.TranscoderInput CreateInput(Input jobInput) => new() { Input = jobInput.FileInput, };

    private static TranscoderJob.TranscoderTiming CreateTiming(Timing timing)
        => new()
        {
            FinishTimeMillis = ((DateTimeOffset)timing.FinishTime).ToUnixTimeMilliseconds(),
            StartTimeMillis = ((DateTimeOffset)timing.StartTime).ToUnixTimeMilliseconds(),
            SubmitTimeMillis = ((DateTimeOffset)timing.SubmitTime).ToUnixTimeMilliseconds(),
        };

    private static List<TranscoderJob.TranscoderOutput> CreateOutputs(Job job, AssetId assetId)
    {
        var jobIsComplete = job.Status == JobStatus.COMPLETE;
        var outputGroupDetails = job.OutputGroupDetails.SingleOrDefault();
        if (outputGroupDetails == null) return []; 
        
        var mediaType = job.UserMetadata[TranscodeMetadataKeys.MediaType]!;

        /* There are 2 related properties: OutputGroupDetails and Settings.OutputGroups.
         The former contains values calculated during encoding: Duration, Width and Height
         The latter contains values provided when creating job: Prefix etc
         Both OutputGroupDetails and Settings.OutputGroups are collections but we only ever have 1 outputGroup to take
         single */
        var outputGroup = job.Settings.OutputGroups.Single();
        var destination = outputGroup.OutputGroupSettings.FileGroupSettings.Destination;

        var transcodeOutputs = new List<TranscoderJob.TranscoderOutput>(outputGroupDetails.OutputDetails.Count);

        for (var x = 0; x < outputGroupDetails.OutputDetails.Count; x++)
        {
            var output = outputGroup.Outputs[x]!;
            var outputDetail = outputGroupDetails.OutputDetails[x]!;

            var storageKeys = GetFinalStorageKeys(destination, output, jobIsComplete, assetId, mediaType);

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

    private static (string TranscodeKey, string? DlcsKey) GetFinalStorageKeys(string destination, Output output,
        bool isComplete, AssetId assetId, string mediaType)
    {
        // Get "Key" part of the destination (s3://timebased-output/1234/2/1/foo/trancode => 1234/2/1/foo/trancode)
        var destinationKey = RegionalisedObjectInBucket.Parse(destination, true)!.Key!;

        // Get key of output (1234/2/1/foo/trancode => 1234/2/1/foo/trancode_1.mp4)
        var outputKey = $"{destinationKey}{output.NameModifier}.{output.Extension}";

        if (!isComplete) return (outputKey, null);

        var storageKey = TranscoderTemplates.GetTranscodeKey(mediaType, assetId, output.Extension);
        return (outputKey, storageKey);
    }
}
