using System;
using DLCS.Model.Assets.NamedQueries;
using DLCS.Repository.Assets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Orchestrator.Features.Manifests;
using Orchestrator.Infrastructure.NamedQueries.Manifest;
using Orchestrator.Infrastructure.NamedQueries.Parsing;
using Orchestrator.Infrastructure.NamedQueries.PDF;
using Orchestrator.Infrastructure.NamedQueries.Persistence;
using Orchestrator.Infrastructure.NamedQueries.Requests;
using Orchestrator.Infrastructure.NamedQueries.Zip;
using Orchestrator.Settings;

namespace Orchestrator.Infrastructure.NamedQueries;

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
    PDF,
    
    /// <summary>
    /// NamedQuery will be projected to ZIP archive containing images.
    /// </summary>
    Zip
}

public static class NamedQueryTypeDeriver
{
    /// <summary>
    /// Derive <see cref="NamedQueryType"/> from type of <see cref="ParsedNamedQuery"/>
    /// </summary>
    public static NamedQueryType GetNamedQueryParser<T>() where T : ParsedNamedQuery
    {
        if (typeof(T) == typeof(IIIFParsedNamedQuery)) return NamedQueryType.IIIF;
        if (typeof(T) == typeof(PdfParsedNamedQuery)) return NamedQueryType.PDF;
        if (typeof(T) == typeof(ZipParsedNamedQuery)) return NamedQueryType.Zip;

        throw new ArgumentOutOfRangeException("Unable to determine NamedQueryType from result type");
    }
}

public delegate INamedQueryParser NamedQueryParserResolver(NamedQueryType projectionType);

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
            .AddScoped<StoredNamedQueryService>()
            .AddScoped<PdfNamedQueryParser>()
            .AddScoped<ZipNamedQueryParser>()
            .AddScoped<NamedQueryResultGenerator>()
            .AddScoped<IProjectionCreator<ZipParsedNamedQuery>, ImageThumbZipCreator>()
            .AddScoped<NamedQueryParserResolver>(provider => outputFormat => outputFormat switch
            {
                NamedQueryType.PDF => provider.GetService<PdfNamedQueryParser>(),
                NamedQueryType.IIIF => provider.GetService<IIIFNamedQueryParser>(),
                NamedQueryType.Zip => provider.GetService<ZipNamedQueryParser>(),
                _ => throw new ArgumentOutOfRangeException(nameof(outputFormat), outputFormat, null)
            });
        
        var fireballRoot = configuration.Get<OrchestratorSettings>().NamedQuery.FireballRoot;
        services.AddHttpClient<IProjectionCreator<PdfParsedNamedQuery>, FireballPdfCreator>(client =>
        {
            client.DefaultRequestHeaders.WithRequestedBy();
            client.BaseAddress = fireballRoot;
        });

        return services;
    }
}