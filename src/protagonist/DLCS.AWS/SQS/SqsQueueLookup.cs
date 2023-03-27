using DLCS.AWS.Settings;
using DLCS.Model.Assets;
using Microsoft.Extensions.Options;

namespace DLCS.AWS.SQS;

public class SqsQueueLookup : IQueueLookup
{
    private readonly SQSSettings sqsOptions;

    public SqsQueueLookup(IOptions<AWSSettings> awsOptions)
    {
        sqsOptions = awsOptions.Value.SQS;
    }
    
    public string GetQueueNameForFamily(AssetFamily family, bool priority = false) 
        => family switch
        {
            AssetFamily.Image => priority ? sqsOptions.PriorityImageQueueName : sqsOptions.ImageQueueName,
            AssetFamily.Timebased => sqsOptions.TimebasedQueueName,
            AssetFamily.File => sqsOptions.FileQueueName,
            _ => throw new ArgumentOutOfRangeException(nameof(family), family, null)
        };
}