using System;
using DLCS.Model.Assets.NamedQueries;
using DLCS.Repository.Assets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Orchestrator.Features.Manifests;
using Orchestrator.Features.PDF;
using Orchestrator.Infrastructure.NamedQueries.Parsing;
using Orchestrator.Infrastructure.NamedQueries.Requests;
using Orchestrator.Settings;

namespace Orchestrator.Infrastructure.NamedQueries
{
    /// <summary>
    /// The type of projection target for named query
    /// </summary>
    public enum NamedQueryType
    {
        /// <summary>
        /// NamedQuery will be projected to IIIF description resource 
        /// </summary>
        IIIF,
        
        /// <summary>
        /// NamedQuery will be projected to PDF object
        /// </summary>
        PDF
    }
        
    public delegate INamedQueryParser  NamedQueryParserResolver(NamedQueryType projectionType);
    
    /// <summary>
    /// <see cref="IServiceCollection"/> extensions for registering OriginStrategy implementations
    /// </summary>
    public static class ServiceCollectionX
    {
        public static IServiceCollection AddNamedQueries(this IServiceCollection services, IConfiguration configuration)
        {
            services
                .AddScoped<IIIFNamedQueryParser>()
                .AddScoped<INamedQueryRepository, NamedQueryRepository>()
                .AddScoped<NamedQueryConductor>()
                .AddScoped<IIIFNamedQueryProjector>()
                .AddScoped<PdfNamedQueryService>()
                .AddScoped<PdfNamedQueryParser>()
                .AddScoped<NamedQueryResultGenerator>()
                .AddScoped<NamedQueryParserResolver>(provider => outputFormat => outputFormat switch
                {
                    NamedQueryType.PDF => provider.GetService<PdfNamedQueryParser>(),
                    NamedQueryType.IIIF => provider.GetService<IIIFNamedQueryParser>(),
                    _ => throw new ArgumentOutOfRangeException(nameof(outputFormat), outputFormat, null)
                });
            
            var fireballRoot = configuration.Get<OrchestratorSettings>().NamedQuery.FireballRoot;
            services.AddHttpClient<IPdfCreator, FireballPdfCreator>(client =>
            {
                client.DefaultRequestHeaders.WithRequestedBy();
                client.BaseAddress = fireballRoot;
            });

            return services;
        }
    }
}