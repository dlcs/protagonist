using DLCS.AWS.S3.Models;
using DLCS.AWS.Transcoding.Models.Request;

namespace DLCS.AWS.MediaConvert.Models;

public class MediaConvertJobGroup(ObjectInBucket destination, IReadOnlyCollection<MediaConvertOutput> outputs)
    : IJobOutput
{
    /// <summary>
    /// Base s3 key for MediaConvert job output
    /// </summary>
    public ObjectInBucket Destination { get; } = destination;

    /// <summary>
    /// List of outputs for MediaConvertJob
    /// </summary>
    public IReadOnlyCollection<MediaConvertOutput> Outputs { get; } = outputs;
}

/// <summary>
/// Represents a single MediaConvert job output within an output group
/// </summary>
/// <param name="Preset">The MediaConvert preset to use for this output</param>
/// <param name="Extension">The extension to use for this output</param>
/// <param name="NameModifier">Optional name modifier for this output</param>
/// <remarks>See https://docs.aws.amazon.com/mediaconvert/latest/ug/using-variables-in-your-job-settings.html for
/// details on built in name modifiers that we could use
/// </remarks>
public record MediaConvertOutput(string Preset, string Extension, string? NameModifier = null);
