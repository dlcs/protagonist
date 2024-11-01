using System;
using DLCS.Model.Assets.NamedQueries;
using DLCS.Repository.Assets;
using DLCS.Repository.NamedQueries.Parsing;
using Microsoft.Extensions.DependencyInjection;

namespace DLCS.Repository.NamedQueries.Infrastructure;

/// <summary>
/// <see cref="IServiceCollection"/> extensions for registering OriginStrategy implementations
/// </summary>
public static class ServiceCollectionX
{
    /// <summary>
    /// Add core/generic NQ processing classes and derivatives.
    /// These are the generic, non projection-specific classes.
    /// </summary>
    public static IServiceCollection AddNamedQueriesCore(this IServiceCollection services)
    {
        services
            .AddScoped<PdfNamedQueryParser>()
            .AddScoped<RawNamedQueryParser>()
            .AddScoped<INamedQueryRepository, NamedQueryRepository>()
            .AddScoped<NamedQueryConductor>()
            .AddScoped<NamedQueryStorageService>()
            .AddScoped<NamedQueryParserResolver>(provider => outputFormat => outputFormat switch
            {
                NamedQueryType.PDF => provider.GetRequiredService<PdfNamedQueryParser>(),
                NamedQueryType.IIIF => provider.GetRequiredService<IIIFNamedQueryParser>(),
                NamedQueryType.Zip => provider.GetRequiredService<ZipNamedQueryParser>(),
                NamedQueryType.Raw => provider.GetRequiredService<RawNamedQueryParser>(),
                _ => throw new ArgumentOutOfRangeException(nameof(outputFormat), outputFormat, null)
            });

        return services;
    }
}