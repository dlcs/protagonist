using System;
using DLCS.Model.Assets.NamedQueries;
using DLCS.Repository.Assets;
using Microsoft.Extensions.DependencyInjection;
using Orchestrator.Features.NamedQueries;
using Orchestrator.Infrastructure.NamedQueries.Parsing;

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
        public static IServiceCollection AddNamedQueries(this IServiceCollection services)
            => services
                .AddTransient<IIIFNamedQueryParser>()
                .AddScoped<INamedQueryRepository, NamedQueryRepository>()
                .AddScoped<NamedQueryConductor>()
                .AddScoped<IIIFNamedQueryProjector>()
                .AddTransient<PdfNamedQueryParser>()
                .AddScoped<NamedQueryParserResolver>(provider => outputFormat => outputFormat switch
                {
                    NamedQueryType.PDF => provider.GetService<PdfNamedQueryParser>(),
                    NamedQueryType.IIIF => provider.GetService<IIIFNamedQueryParser>(),
                    _ => throw new ArgumentOutOfRangeException(nameof(outputFormat), outputFormat, null)
                });
    }
}