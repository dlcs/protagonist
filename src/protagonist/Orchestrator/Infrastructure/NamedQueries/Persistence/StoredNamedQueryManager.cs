using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Collections;
using DLCS.Core.Guard;
using DLCS.Core.Streams;
using DLCS.Core.Types;
using DLCS.Model.Assets.NamedQueries;
using DLCS.Repository.NamedQueries;
using DLCS.Repository.NamedQueries.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestrator.Infrastructure.Auth;
using Orchestrator.Infrastructure.NamedQueries.Persistence.Models;
using Orchestrator.Settings;

namespace Orchestrator.Infrastructure.NamedQueries.Persistence;

/// <summary>
/// Service for handling NQ projections that are created and stored alongside a corresponding control-file.
/// </summary>
public class StoredNamedQueryManager(
    NamedQueryStorageService namedQueryStorageService,
    IOptions<NamedQuerySettings> namedQuerySettings,
    ILogger<StoredNamedQueryManager> logger,
    IAssetAccessValidator assetAccessValidator)
{
    private readonly NamedQuerySettings namedQuerySettings = namedQuerySettings.Value;

    /// <summary>
    /// Get <see cref="StoredResult"/> containing data stream and status for specific named query result.
    /// </summary>
    public async Task<StoredResult> GetResults<T>(NamedQueryResult<T> namedQueryResult,
        IProjectionCreator<T> projectionCreator, bool validateRoles, 
        CancellationToken cancellationToken = default)
        where T : StoredParsedNamedQuery
    {
        namedQueryResult.ParsedQuery.ThrowIfNull(nameof(namedQueryResult.ParsedQuery));

        var parsedNamedQuery = namedQueryResult.ParsedQuery!;

        // Check to see if we can use an existing item
        var existingResult =
            await TryGetExistingResource(parsedNamedQuery, validateRoles, projectionCreator, cancellationToken);
        
        // If it's Found or InProcess then no further processing for now, returns what's found
        if (existingResult.Status is PersistedProjectionStatus.Available or PersistedProjectionStatus.InProcess
            or PersistedProjectionStatus.Restricted)
        {
            return existingResult;
        }

        // If we hit here there is no projection - create one
        var imageResults = await namedQueryResult.Results
            .IncludeRelevantMetadata()
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
        if (imageResults.Count == 0)
        {
            logger.LogWarning("No results found for stored file {S3StorageKey}, aborting", parsedNamedQuery.StorageKey);
            return new StoredResult(Stream.Null, PersistedProjectionStatus.NotFound, existingResult.RequiresAuth);
        }

        var (success, controlFile) =
            await projectionCreator.PersistProjection(parsedNamedQuery, imageResults, cancellationToken);
        if (!success)
        {
            return new StoredResult(Stream.Null, PersistedProjectionStatus.Error, existingResult.RequiresAuth);
        }

        var requiresAuth = !controlFile!.Roles.IsNullOrEmpty();
        
        if (validateRoles && requiresAuth)
        {
            if (!await CanUserViewItem(parsedNamedQuery, controlFile))
            {
                return new(Stream.Null, PersistedProjectionStatus.Restricted, requiresAuth);
            }
        }

        var projection = await namedQueryStorageService.LoadProjection(parsedNamedQuery, cancellationToken);
        if (!projection.Stream.IsNull())
        {
            return new(projection.Stream, PersistedProjectionStatus.Available, requiresAuth);
        }

        logger.LogWarning("File {S3Key} was successfully created but now cannot be loaded",
            parsedNamedQuery.StorageKey);
        return new(Stream.Null, PersistedProjectionStatus.Error, requiresAuth);
    }

    private async Task<StoredResult> TryGetExistingResource<T>(T parsedNamedQuery,
        bool validateRoles, IProjectionCreator<T> projectionCreator, CancellationToken cancellationToken)
        where T : StoredParsedNamedQuery
    {
        var controlFile = await namedQueryStorageService.GetControlFile(parsedNamedQuery, cancellationToken);
        if (controlFile == null) return new(Stream.Null, PersistedProjectionStatus.NotFound, null);

        var itemKey = parsedNamedQuery.StorageKey;
        var requiresAuth = !controlFile.Roles.IsNullOrEmpty();
        
        if (validateRoles && requiresAuth && !await CanUserViewItem(parsedNamedQuery, controlFile))
        {
            return new(Stream.Null, PersistedProjectionStatus.Restricted, true);
        }

        if (controlFile.IsStale(namedQuerySettings.ControlStaleSecs))
        {
            return await HandleStaleControlFile(parsedNamedQuery, projectionCreator, controlFile, itemKey, requiresAuth, cancellationToken);
        }

        if (controlFile.InProcess)
        {
            logger.LogWarning("File {S3Key} has valid control-file but it's in progress", itemKey);
            return new(Stream.Null, PersistedProjectionStatus.InProcess, requiresAuth);
        }

        var resource = await namedQueryStorageService.LoadProjection(parsedNamedQuery, cancellationToken);
        if (!resource.Stream.IsNull())
        {
            return new(resource.Stream, PersistedProjectionStatus.Available, requiresAuth);
        }

        logger.LogWarning("File {S3Key} has valid control-file but item not found. Will recreate", itemKey);
        return new(Stream.Null, PersistedProjectionStatus.NotFound, requiresAuth);
    }

    private async Task<bool> CanUserViewItem(StoredParsedNamedQuery parsedNamedQuery, ControlFile controlFile)
    {
        var mockAssetId = GetMockAssetId(parsedNamedQuery);
        var access = await assetAccessValidator.TryValidate(mockAssetId, controlFile.Roles ?? new List<string>(),
            AuthMechanism.Cookie);
        return access is AssetAccessResult.Open or AssetAccessResult.Authorized;
    }
    
    private async Task<StoredResult> HandleStaleControlFile<T>(T parsedNamedQuery,
        IProjectionCreator<T> projectionCreator, ControlFile controlFile, string itemKey, bool requiresAuth,
        CancellationToken cancellationToken)
        where T : StoredParsedNamedQuery
    {
        // If control-file is stale, check to see if the downstream service completed eventually - if this is the case
        // then the projection will exist and will have been created after the control-file 
        var projection = await namedQueryStorageService.LoadProjection(parsedNamedQuery, cancellationToken);
        if (!projection.Stream.IsNull() && controlFile.Created < projection.Headers.LastModified)
        {
            logger.LogInformation("File {S3Key} has stale control-file but projection exists, updating control file",
                itemKey);

            await projectionCreator.MarkControlFileComplete(parsedNamedQuery, controlFile,
                projection.Headers.ContentLength ?? 0, cancellationToken);
            return new(projection.Stream, PersistedProjectionStatus.Available, requiresAuth);
        }

        logger.LogWarning("File {S3Key} has valid control-file but it is stale. Will recreate",
            itemKey);
        return new(Stream.Null, PersistedProjectionStatus.NotFound, requiresAuth);
    }

    /// <summary>
    /// Validation relies on AssetId but for NQs we don't have that, we only have a CustomerId. This is enough to
    /// perform validation so use dummy space + asset to generate AssetId 
    /// </summary>
    private static AssetId GetMockAssetId(StoredParsedNamedQuery parsedNamedQuery)
    {
        const int placeholderSpace = -1;
        const string placeholderAsset = "_namedquery_";
        return new AssetId(parsedNamedQuery.Customer, placeholderSpace, placeholderAsset);
    }
}

public record StoredResult(Stream? Stream, PersistedProjectionStatus Status, bool? RequiresAuth);
