namespace DLCS.AWS.Settings;

public class TranscodeSettings
{
    /// <summary>
    /// Name of the MediaConvert queue to use
    /// </summary>
    public string QueueName { get; set; } = null!;

    /// <summary>
    /// Arn of role to use for MediaConvert queue to use
    /// </summary>
    public string RoleArn { get; set; } = null!;

    /// <summary>
    /// Mapping values for policy-data name to preset+extension. e.g.
    /// { "audio-mp3" : "SystemPreset_foo_bar_q1|wav" }
    /// </summary>
    public Dictionary<string, string> DeliveryChannelMappings { get; set; } = new();
}
