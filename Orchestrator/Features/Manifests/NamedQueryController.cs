using System.Threading;
using System.Threading.Tasks;
using DLCS.Repository.Caching;
using DLCS.Web.IIIF;
using IIIF.Presentation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestrator.Features.Manifests.Requests;
using Orchestrator.Infrastructure;
using Orchestrator.Settings;

namespace Orchestrator.Features.Manifests
{
    [Route("iiif-resource")]
    [ApiController]
    public class NamedQueryController : IIIFAssetControllerBase
    {
        private readonly OrchestratorSettings orchestratorSettings;

        public NamedQueryController(
            IMediator mediator, 
            IOptions<CacheSettings> cacheSettings,
            IOptions<OrchestratorSettings> orchestratorSettings,
            ILogger<NamedQueryController> logger
        ) : base(mediator, cacheSettings, logger)
        {
            this.orchestratorSettings = orchestratorSettings.Value;
        }

        /// <summary>
        /// Get results of named query with specified name. The IIIF Presentation version returned will depend on server
        /// configuration but can be specified via the Accepts header
        /// </summary>
        /// <returns>IIIF manifest containing results of specified named query</returns>
        [Route("{customer}/{namedQueryName}/{**namedQueryArgs}")]
        [HttpGet]
        public Task<IActionResult> Index(string customer, string namedQueryName, string? namedQueryArgs = null,
            CancellationToken cancellationToken = default)
        {
            var version =
                Request.GetIIIFPresentationApiVersion(orchestratorSettings.GetDefaultIIIFPresentationVersion());
            return RenderNamedQuery(customer, namedQueryName, namedQueryArgs, version, cancellationToken);
        }

        /// <summary>
        /// Get results of named query with specified name as a IIIF Manifest conforming to v2.1.
        /// </summary>
        /// <returns>IIIF manifest containing results of specified named query</returns>
        [Route("v2/{customer}/{namedQueryName}/{**namedQueryArgs}")]
        public Task<IActionResult> V2(string customer, string namedQueryName, string? namedQueryArgs = null,
            CancellationToken cancellationToken = default)
            => RenderNamedQuery(customer, namedQueryName, namedQueryArgs, Version.V2, cancellationToken);

        /// <summary>
        /// Get results of named query with specified name as a IIIF Manifest conforming to v3.0.
        /// </summary>
        /// <returns>IIIF manifest containing results of specified named query</returns>
        [Route("v3/{customer}/{namedQueryName}/{**namedQueryArgs}")]
        public Task<IActionResult> V3(string customer, string namedQueryName, string? namedQueryArgs = null,
            CancellationToken cancellationToken = default)
            => RenderNamedQuery(customer, namedQueryName, namedQueryArgs, Version.V3, cancellationToken);

        public Task<IActionResult> RenderNamedQuery(string customer, string namedQueryName, string? namedQueryArgs,
            Version presentationVersion, CancellationToken cancellationToken = default)
            => GenerateIIIFDescriptionResource(
                () => new GetNamedQueryResults(customer, namedQueryName, namedQueryArgs, presentationVersion),
                Request.GetIIIFContentType(presentationVersion),
                cancellationToken);
    }
}