using DLCS.Core;
using DLCS.Model.Assets;
using Engine.Ingest.File;
using Engine.Ingest.Image;
using Engine.Ingest.Timebased;

namespace Engine.Ingest;

public interface IWorkerBuilder
{
    IReadOnlyCollection<IAssetIngesterWorker> GetWorkers(Asset asset);
}

/// <summary>
/// Class responsible for generating a list of workers required to ingest asset 
/// </summary>
public class WorkerBuilder : IWorkerBuilder
{
    private readonly IServiceProvider serviceProvider;
    private readonly ILogger<WorkerBuilder> logger;

    public WorkerBuilder(IServiceProvider serviceProvider, ILogger<WorkerBuilder> logger)
    {
        this.serviceProvider = serviceProvider;
        this.logger = logger;
    }
    
    public IReadOnlyCollection<IAssetIngesterWorker> GetWorkers(Asset asset)
    {
        var workers = new List<IAssetIngesterWorker>();

        void AddProcessor(IAssetIngesterWorker worker)
        {
            if (workers.Any(p => p.GetType() == worker.GetType())) return;
        
            workers.Add(worker);
        }

        /*
         * NOTE: Ordering of handlers is important - File should run first.
         * 
         * - "file" may upload origin to DLCS. If that's the case then further handlers may not need to, or can use
         * that info in their processing
         * - "images" can need to store origin file, but this may have been done by File so results in no-op
         * - "timebased" will source file size in ET user-metadata if known, this will ahve been set by "file"
         */
        if (asset.HasDeliveryChannel(AssetDeliveryChannels.File))
        {
            AddProcessor(serviceProvider.GetRequiredService<FileChannelWorker>());
        }
        
        if (MIMEHelper.IsImage(asset.MediaType))
        {
            if (asset.HasDeliveryChannel(AssetDeliveryChannels.Image))
            {
                AddProcessor(serviceProvider.GetRequiredService<ImageIngesterWorker>());
            }
        }

        if (MIMEHelper.IsVideo(asset.MediaType) || MIMEHelper.IsAudio(asset.MediaType))
        {
            if (asset.HasDeliveryChannel(AssetDeliveryChannels.Timebased))
            {
                AddProcessor(serviceProvider.GetRequiredService<TimebasedIngesterWorker>());
            }
        }

        if (logger.IsEnabled(LogLevel.Trace))
        {
            logger.LogTrace("Asset {AssetId} to be processed by {Processors}", asset.Id,
                workers.Select(p => p.GetType()));
        }

        return workers;
    }
}