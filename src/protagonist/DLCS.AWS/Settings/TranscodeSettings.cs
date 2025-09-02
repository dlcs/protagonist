namespace DLCS.AWS.Settings;

public class TranscodeSettings
{
    /// <summary>
    /// Name of the MediaConvert queue to use
    /// </summary>
    public required string QueueName { get; set; }

    /// <summary>
    /// Arn of role to use for MediaConvert queue to use
    /// </summary>
    public required string RoleArn { get; set; }
    
    /// <summary>
    /// Mapping values for policy-data name to preset+extension. e.g.
    /// { "audio-mp3" : "SystemPreset_foo_bar_q1|wav" }
    /// </summary>
    public Dictionary<string, string> DeliveryChannelMappings { get; set; }
}
