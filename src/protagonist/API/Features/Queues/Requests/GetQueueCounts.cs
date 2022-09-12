using System.Threading;
using System.Threading.Tasks;
using API.Infrastructure.Requests;
using DLCS.AWS.Settings;
using DLCS.AWS.SQS;
using MediatR;
using Microsoft.Extensions.Options;

namespace API.Features.Queues.Requests;

/// <summary>
/// Handler for getting overall queue counts from SQS
/// </summary>
/// <remarks>This is not customer specific</remarks>
public class GetQueueCounts : IRequest<FetchEntityResult<QueueCount>>
{
    public GetQueueCounts()
    {
    }
}

public class QueueCount
{
    public int Incoming { get; set; }
    public int Priority { get; set; }
    public int Timebased { get; set; }
    public int TranscodeComplete { get; set; }
}

public class GetQueueCountsHandler : IRequestHandler<GetQueueCounts, FetchEntityResult<QueueCount>>
{
    private readonly SqsQueueUtilities sqsQueueUtilities;
    private readonly AWSSettings awsSettings;

    public GetQueueCountsHandler(
        SqsQueueUtilities sqsQueueUtilities,
        IOptions<AWSSettings> awsOptions)
    {
        this.sqsQueueUtilities = sqsQueueUtilities;
        awsSettings = awsOptions.Value;
    }
    
    public async Task<FetchEntityResult<QueueCount>> Handle(GetQueueCounts request, CancellationToken cancellationToken)
    {
        var awsSettingsSQS = awsSettings.SQS;
        var results = new QueueCount();
        results.Incoming = await GetApproximateQueueCounts(awsSettingsSQS.ImageQueueName, cancellationToken);
        results.Priority = await GetApproximateQueueCounts(awsSettingsSQS.PriorityImageQueueName, cancellationToken);
        results.Timebased = await GetApproximateQueueCounts(awsSettingsSQS.TimebasedQueueName, cancellationToken);
        results.TranscodeComplete =
            await GetApproximateQueueCounts(awsSettingsSQS.TranscodeCompleteQueueName, cancellationToken);

        return FetchEntityResult<QueueCount>.Success(results);
    }

    private async Task<int> GetApproximateQueueCounts(string? queueName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(queueName)) return 0;

        var approx = await sqsQueueUtilities.GetApproximateTotalMessages(queueName, cancellationToken);
        return approx ?? 0;
    }
}