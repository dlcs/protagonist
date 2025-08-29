using Amazon.ElasticTranscoder.Model;
using DLCS.AWS.Transcoding;
using DLCS.AWS.Transcoding.Models.Job;
using DLCS.Core.Types;

namespace DLCS.AWS.MediaConvert;

public class MediaConvertWrapper : ITranscoderWrapper
{
    public Task<string?> GetPipelineId(string pipelineName, CancellationToken token = default)
    {
        throw new NotImplementedException();
    }

    public Task<CreateJobResponse> CreateJob(string inputKey, string pipelineId, List<CreateJobOutput> outputs, Dictionary<string, string> jobMetadata,
        CancellationToken token = default)
    {
        throw new NotImplementedException();
    }

    public Task PersistJobId(AssetId assetId, string transcoderJobId, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<TranscoderJob?> GetTranscoderJob(AssetId assetId, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
