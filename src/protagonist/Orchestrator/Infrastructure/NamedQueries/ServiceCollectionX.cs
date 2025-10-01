using System;
using DLCS.Model.Assets.NamedQueries;
using DLCS.Repository.NamedQueries.Infrastructure;
using DLCS.Repository.NamedQueries.Parsing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Orchestrator.Features.Manifests;
using Orchestrator.Infrastructure.NamedQueries.PDF;
using Orchestrator.Infrastructure.NamedQueries.Persistence;
using Orchestrator.Infrastructure.NamedQueries.Requests;
using Orchestrator.Infrastructure.NamedQueries.Zip;
using Orchestrator.Settings;

namespace Orchestrator.Infrastructure.NamedQueries;

/// <summary>
/// <see cref="IServiceCollection"/> extensions for registering OriginStrategy implementations
/// </summary>
public static class ServiceCollectionX
{
    public static IServiceCollection AddNamedQueries(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddNamedQueriesCore()
            .AddScoped<IIIFNamedQueryParser>()
            .AddScoped<IIIFNamedQueryProjector>()
            .AddScoped<StoredNamedQueryManager>()
            .AddScoped<PdfNamedQueryParser>()
            .AddScoped<ZipNamedQueryParser>()
            .AddScoped<NamedQueryResultGenerator>()
            .AddScoped<IProjectionCreator<ZipParsedNamedQuery>, ImageThumbZipCreator>();

        var namedQuerySettings = configuration.Get<OrchestratorSettings>()?.NamedQuery;
        var fireballRoot = namedQuerySettings?.FireballRoot;
        var fireballTimeout = namedQuerySettings?.FireballTimeoutMs ?? 60;
        services.AddHttpClient<IProjectionCreator<PdfParsedNamedQuery>, FireballPdfCreator>(client =>
        {
            client.DefaultRequestHeaders.WithRequestedBy();
            client.BaseAddress = fireballRoot;
            client.Timeout = TimeSpan.FromMilliseconds(fireballTimeout);
        });

        return services;
    }
}
