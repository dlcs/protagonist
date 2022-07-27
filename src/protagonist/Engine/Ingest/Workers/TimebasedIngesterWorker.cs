using System.Diagnostics;
using DLCS.Model.Customers;
using DLCS.Model.Messaging;
using Engine.Ingest.Workers.Persistence;
using Engine.Settings;
using Microsoft.Extensions.Options;

namespace Engine.Ingest.Workers;

/// <summary>
/// <see cref="IAssetIngesterWorker"/> responsible for ingesting timebased assets ('T' family)  
/// </summary>
public class TimebasedIngesterWorker : IAssetIngesterWorker
{
    private readonly AssetToS3 assetMover;
    //private readonly IMediaTranscoder mediaTranscoder;
    //private readonly ITimebasedIngestorCompletion completion;
    private readonly EngineSettings engineSettings;
    private readonly ILogger<TimebasedIngesterWorker> logger;
     
    
    public TimebasedIngesterWorker(
        AssetToS3 assetToS3,
        IOptionsMonitor<EngineSettings> engineOptions,
        //IMediaTranscoder mediaTranscoder,
        //ITimebasedIngestorCompletion completion,
        ILogger<TimebasedIngesterWorker> logger)
    {
        //this.mediaTranscoder = mediaTranscoder;
        //this.completion = completion;
        engineSettings = engineOptions.CurrentValue;
        this.logger = logger;
    }
    
    public async Task<IngestResult> Ingest(IngestAssetRequest ingestAssetRequest,
        CustomerOriginStrategy customerOriginStrategy, CancellationToken cancellationToken = default)
    {
        try
        {
            var stopwatch = Stopwatch.StartNew();
            throw new NotImplementedException();
        }
        catch (Exception ex)
        {
            // TODO - set context.Asset.Error and save
            logger.LogError(ex, "Error ingesting timebased asset {AssetId}", ingestAssetRequest.Asset.Id);
            return IngestResult.Failed;
        }
    }
}