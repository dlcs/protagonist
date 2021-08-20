using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Threading;
using DLCS.Core.Types;
using DLCS.Model.Templates;
using DLCS.Repository.Strategy;
using DLCS.Web.Requests.AssetDelivery;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestrator.Assets;
using Orchestrator.Settings;

namespace Orchestrator.Features.Images.Commands
{
    public class OrchestrateImage : IRequest<bool>
    {
        public OrchestrationImage OrchestrationAsset { get; }
        
        public ImageAssetDeliveryRequest AssetRequest { get; }

        public OrchestrateImage(OrchestrationImage orchestrationAsset)
        {
            OrchestrationAsset = orchestrationAsset;
        }
    }
    
    public class OrchestrateImageHandler : IRequestHandler<OrchestrateImage, bool>
    {
        private readonly IAssetTracker assetTracker;
        private readonly ImageOrchestrator orchestrator;

        public OrchestrateImageHandler(IAssetTracker assetTracker, ImageOrchestrator orchestrator)
        {
            this.assetTracker = assetTracker;
            this.orchestrator = orchestrator;
        }
        
        public async Task<bool> Handle(OrchestrateImage request, CancellationToken cancellationToken)
        {
            /* TODO - this request becomes a call back out to Deliverator to serve an 'open' image
             * OrchestrationAsset should define _where_ an image is (Orchestrating, Orchestrated, NotOrchestrated)
             * Start from a basic point of using File.Exists, them memCache/redis
             * Use asyncLocker for basic implementation - RedisLock can come after             
             * */
            
            if (request.OrchestrationAsset.Status == OrchestrationStatus.Orchestrated) return true;
            
            await orchestrator.OrchestrateImage(request.OrchestrationAsset, cancellationToken);

            throw new System.NotImplementedException();
        }
    }

    public class ImageOrchestrator
    {
        private readonly IAssetTracker assetTracker;
        private readonly IOptions<OrchestratorSettings> orchestratorSettings;
        private readonly OriginFetcher originFetcher;
        private readonly FileSaver fileSaver;
        private readonly ILogger<ImageOrchestrator> logger;
        private readonly AsyncKeyedLock asyncLocker = new();

        public ImageOrchestrator(IAssetTracker assetTracker,
            IOptions<OrchestratorSettings> orchestratorSettings,
            OriginFetcher originFetcher,
            FileSaver fileSaver,
            ILogger<ImageOrchestrator> logger)
        {
            this.assetTracker = assetTracker;
            this.orchestratorSettings = orchestratorSettings;
            this.originFetcher = originFetcher;
            this.fileSaver = fileSaver;
            this.logger = logger;
        }
        
        // this can have asyncKeyedLocker and move one at a time
        public async Task OrchestrateImage(OrchestrationImage orchestrationImage, 
            CancellationToken cancellationToken = default)
        {
            // Safety check - final check if item is orchestrated before doing any work
            if (GetCurrentStatus(orchestrationImage.AssetId) == OrchestrationStatus.Orchestrated)
            {
                logger.LogInformation("Asset '{AssetId}' is already orchestrated", orchestrationImage.AssetId);
                await assetTracker.TrySetOrchestrationStatus(orchestrationImage, OrchestrationStatus.Orchestrated,
                    true, cancellationToken);
                return;
            }
            
            using (var updateLock = await GetLock(orchestrationImage.AssetId.ToString()))
            {
                var (saveSuccess, currentOrchestrationImage) =
                    await assetTracker.TrySetOrchestrationStatus(orchestrationImage, OrchestrationStatus.Orchestrating,
                        cancellationToken: cancellationToken);

                if (!saveSuccess && currentOrchestrationImage.Status == OrchestrationStatus.Orchestrated)
                {
                    // Previous lock holder has orchestrated image, abort.
                    return;
                }
                
                if (string.IsNullOrEmpty(orchestrationImage.S3Location))
                {
                    throw new NotImplementedException("Resync asset and refresh cached version");
                }

                await SaveFileToDisk(orchestrationImage, cancellationToken);

                // Save status as Orchestrated
                await assetTracker.TrySetOrchestrationStatus(orchestrationImage, OrchestrationStatus.Orchestrated,
                    true, cancellationToken);
            }
        }

        private async Task SaveFileToDisk(OrchestrationImage orchestrationImage, CancellationToken cancellationToken)
        {
            // Get bytes from S3
            await using (var originResponse = await originFetcher.LoadAssetFromLocation(orchestrationImage.AssetId,
                orchestrationImage.S3Location, cancellationToken))
            {
                if (originResponse == null || originResponse.Stream == Stream.Null)
                {
                    // TODO correct type of exception?
                    logger.LogWarning("Unable to get asset {Asset} from {Origin}", orchestrationImage.AssetId,
                        orchestrationImage.S3Location);
                    throw new ApplicationException(
                        $"Unable to get asset '{orchestrationImage.AssetId}' from origin");
                }
                
                var targetPath = TemplatedFolders.GenerateTemplate(orchestratorSettings.Value.ImageFolderTemplate,
                    string.Empty, orchestrationImage.AssetId);

                // Save bytes to disk
                await fileSaver.SaveResponseToDisk(orchestrationImage.AssetId, originResponse, targetPath,
                    cancellationToken);
            }
        }

        // TODO - change timeout logic to check asset size
        private Task<IDisposable> GetLock(string key)
            => asyncLocker.LockAsync(
                string.Intern(key), 
                TimeSpan.FromMilliseconds(orchestratorSettings.Value.CriticalPathTimeoutMs));

        public OrchestrationStatus GetCurrentStatus(AssetId assetId)
        {
            var localPath = GetLocalPath(assetId);
            if (File.Exists(localPath)) return OrchestrationStatus.Orchestrated;
            
            // How to tell if Orchestrating? Use Asynclocker here?

            return OrchestrationStatus.NotOrchestrated;
        }
        
        private string GetLocalPath(AssetId assetId)
        {
            // TODO - this should be in engine_rework branch
            return "";
        }
    }
}