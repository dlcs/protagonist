using DLCS.AWS.Settings;
using DLCS.Core.Guard;
using DLCS.Model.Assets;
using Microsoft.Extensions.Options;

namespace DLCS.AWS.SQS;

public class SqsQueueLookup(IOptions<AWSSettings> awsOptions) : IQueueLookup
{
    private readonly SQSSettings sqsOptions = awsOptions.Value.SQS;

    public string GetQueueNameForFamily(AssetFamily family, bool priority = false)
        => family switch
        {
            AssetFamily.Image => priority ? sqsOptions.PriorityImageQueueName : sqsOptions.ImageQueueName,
            AssetFamily.Timebased => sqsOptions.TimebasedQueueName,
            AssetFamily.File => sqsOptions.FileQueueName,
            _ => throw new ArgumentOutOfRangeException(nameof(family), family, null)
        };

    public string GetAdjunctsQueueName() =>
        sqsOptions.AdjunctQueueName.ThrowIfNull("Tried to get adjuncts queue name but it was not correctly configured");
}
