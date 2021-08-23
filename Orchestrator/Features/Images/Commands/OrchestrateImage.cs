using System.Threading;
using System.Threading.Tasks;
using DLCS.Mediatr.Behaviours;
using MediatR;
using Microsoft.Extensions.Logging;
using Orchestrator.Assets;

namespace Orchestrator.Features.Images.Commands
{
    /// <summary>
    /// Mediatr request for orchestrating an image from S3 storage to fast disk.
    /// </summary>
    public class OrchestrateImage : ITimedRequest
    {
        public OrchestrationImage Image { get; }
        
        public OrchestrateImage(OrchestrationImage image)
        {
            Image = image;
        }

        public LogLevel? LoggingLevel { get; } = LogLevel.Information;

        public override string ToString() => $"Orchestrate: {Image.AssetId}";
    }
    
    public class OrchestrateImageHandler : IRequestHandler<OrchestrateImage>
    {
        private readonly ImageOrchestrator orchestrator;
        private readonly ILogger<OrchestrateImageHandler> logger;

        public OrchestrateImageHandler(ImageOrchestrator orchestrator,
            ILogger<OrchestrateImageHandler> logger)
        {
            this.orchestrator = orchestrator;
            this.logger = logger;
        }
        
        public async Task<Unit> Handle(OrchestrateImage request, CancellationToken cancellationToken)
        {
            if (request.Image.Status == OrchestrationStatus.Orchestrated)
            {
                logger.LogDebug("Asset '{AssetId}' already orchestrated, aborting", request.Image.AssetId);
                return Unit.Value;
            }
            
            await orchestrator.OrchestrateImage(request.Image, cancellationToken);
            return Unit.Value;
        }
    }
}