using System.Text.RegularExpressions;
using Amazon.ElasticTranscoder.Model;

namespace DLCS.AWS.Transcoding.Models.Job;

/// <summary>
/// Classes that represent a elastictranscoder:ReadJob request
/// </summary>
/// <remarks>
/// The output classes here match those from Deliverator. They also very closely match the output from the ET request
/// but maintaining the separate classes so that we have control over what is returned.
/// </remarks>
public class TranscoderJob
{
    public string Id { get; private init; }
    public string OutputKeyPrefix { get; private init; }
    public TranscoderInput Input { get; private init; }
    public IEnumerable<TranscoderInput> Inputs { get; private init; }

    public TranscoderOutput Output { get; private init; }
    public IEnumerable<TranscoderOutput> Outputs { get; private init; }

    public string PipelineId { get; private init; }
    public string Status { get; private init; }
    public TranscoderTiming Timing { get; private init; }
    public Dictionary<string, string> UserMetadata { get; private init; }
    
    public IEnumerable<TranscoderPlaylist> Playlists { get; private init; }

    public static TranscoderJob Create(Amazon.ElasticTranscoder.Model.Job job)
    {
        job.UserMetadata.TryGetValue(TranscodeMetadataKeys.JobId, out var dlcsJobId);
        dlcsJobId ??= "-not-found-";

        var etJob = new TranscoderJob
        {
            Id = job.Id,
            OutputKeyPrefix = job.OutputKeyPrefix,
            Status = job.Status,
            PipelineId = job.PipelineId,
            Output = TranscoderOutput.Create(job.Output, dlcsJobId),
            Outputs = job.Outputs.Select(o => TranscoderOutput.Create(o, dlcsJobId)),
            Input = TranscoderInput.Create(job.Input),
            Inputs = job.Inputs.Select(i => TranscoderInput.Create(i)),
            Timing = TranscoderTiming.Create(job.Timing),
            UserMetadata = job.UserMetadata,
            Playlists = job.Playlists.Select(p => TranscoderPlaylist.Create(p)),
        };

        return etJob;
    }

    public class TranscoderInput
    {
        public string AspectRatio { get; private init; }
        public string Container { get; private init; }
        public string FrameRate { get; private init; }
        public string Interlaced { get; private init; }
        public string Key { get; private init; }
        public string Resolution { get; private init; }

        public static TranscoderInput Create(JobInput jobInput)
            => new()
            {
                AspectRatio = jobInput.AspectRatio,
                Container = jobInput.Container,
                FrameRate = jobInput.FrameRate,
                Interlaced = jobInput.Interlaced,
                Key = jobInput.Key,
                Resolution = jobInput.Resolution,
            };
    }

    public class TranscoderOutput
    {
        public string Id { get; private init; }
        public long Duration { get; private init; }
        public long DurationMillis { get; private init; }
        public long FileSize { get; private init; }
        public int Height { get; private init; }
        public int Width { get; private init; }
        public string Key { get; private init; }
        public string Status { get; private init; }
        public string StatusDetail { get; private init; }

        public static TranscoderOutput Create(JobOutput jobOutput, string dlcsJobId)
            => new()
            {
                Id = jobOutput.Id,
                Duration = jobOutput.Duration,
                DurationMillis = jobOutput.DurationMillis,
                FileSize = jobOutput.FileSize,
                Height = jobOutput.Height,
                Width = jobOutput.Width,
                Status = jobOutput.Status,
                Key = GetOutputKey(jobOutput, dlcsJobId),
                StatusDetail = jobOutput.StatusDetail,
            };

        private static string GetOutputKey(JobOutput jobOutput, string dlcsJobId)
        {
            // fixup output key for completed jobs as ET output doesn't match final location (Engine moves it)
            if (string.Equals(jobOutput.Status, "Complete", StringComparison.OrdinalIgnoreCase))
            {
                // Current, prefix is JobId metadata value
                // e.g. ac232ab4-c123-4a68-8562-2d9f1a7908fa/2/1/asset-id/full/full/max/max/0/default.mp4
                //   ->                                      2/1/asset-id/full/full/max/max/0/default.mp4
                if (jobOutput.Key.StartsWith(dlcsJobId))
                {
                    return jobOutput.Key[(dlcsJobId.Length + 1)..];
                }

                // Legacy (Deliverator via Spacebunny)
                // e.g. x/0127/2/1/asset-id/full/full/max/max/0/default.mp4
                //   ->        2/1/asset-id/full/full/max/max/0/default.mp4
                var deliveratorRegex = new Regex(@"^x\/\d+\/(.*)$");
                if (deliveratorRegex.IsMatch(jobOutput.Key))
                {
                    return deliveratorRegex.Match(jobOutput.Key).Groups[1].Value;
                }
            }

            return jobOutput.Key;
        }
    }

    public class TranscoderTiming
    {
        public long FinishTimeMillis { get; private init; }
        public long StartTimeMillis { get; private init; }
        public long SubmitTimeMillis { get; private init; }

        public static TranscoderTiming Create(Timing timing)
            => new()
            {
                FinishTimeMillis = timing.FinishTimeMillis,
                StartTimeMillis = timing.StartTimeMillis,
                SubmitTimeMillis = timing.SubmitTimeMillis
            };
    }

    public class TranscoderPlaylist
    {
        public string Format { get; set; }
        public string Name { get; set; }
        public IEnumerable<string> OutputKeys { get; set; }
        public string Status { get; set; }
        public string StatusDetail { get; set; }

        public static TranscoderPlaylist Create(Playlist playlist)
            => new()
            {
                Format = playlist.Format,
                Name = playlist.Name,
                Status = playlist.Status,
                OutputKeys = playlist.OutputKeys,
                StatusDetail = playlist.StatusDetail
            };
    }
}
