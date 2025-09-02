namespace Engine.Ingest.Timebased.Models;

[Obsolete("Redundant after MediaConvert")]
public class TimeBasedPolicy
{
    public TimeBasedPolicy(string policy)
    {
        var policySplit = policy.Split('-');

        ChannelType = policySplit[0];
        Extension = policySplit[1];
    }
    
    public string ChannelType { get; init; }

    public string Extension { get; init; }
}
