﻿using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Collections;
using DLCS.Core.Guard;
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
public class StoredNamedQueryManager
{
    private readonly NamedQueryStorageService namedQueryStorageService;
    private readonly ILogger<StoredNamedQueryManager> logger;
    private readonly IAssetAccessValidator assetAccessValidator;
    private readonly NamedQuerySettings namedQuerySettings;

    public StoredNamedQueryManager(
        NamedQueryStorageService namedQueryStorageService,
        IOptions<NamedQuerySettings> namedQuerySettings,
        ILogger<StoredNamedQueryManager> logger, 
        IAssetAccessValidator assetAccessValidator)
    {
        this.namedQueryStorageService = namedQueryStorageService;
        this.namedQuerySettings = namedQuerySettings.Value;
        this.logger = logger;
        this.assetAccessValidator = assetAccessValidator;
    }

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
        var existingResult = await TryGetExistingResource(parsedNamedQuery, validateRoles, cancellationToken);

        // If it's Found or InProcess then no further processing for now, returns what's found
        if (existingResult.Status is PersistedProjectionStatus.Available or PersistedProjectionStatus.InProcess
            or PersistedProjectionStatus.Restricted)
        {
            return existingResult;
        }

        var imageResults = await namedQueryResult.Results.ToListAsync(cancellationToken);
        if (imageResults.Count == 0)
        {
            logger.LogWarning("No results found for stored file {S3StorageKey}, aborting", parsedNamedQuery.StorageKey);
            return new StoredResult(Stream.Null, PersistedProjectionStatus.NotFound, existingResult.RequiresAuth);
        }

        var (success, controlFile) =
            await projectionCreator.PersistProjection(parsedNamedQuery, imageResults, cancellationToken);
        if (!success)
            return new StoredResult(Stream.Null, PersistedProjectionStatus.Error, existingResult.RequiresAuth);
        
        var requiresAuth = !controlFile!.Roles.IsNullOrEmpty();
        
        if (validateRoles && requiresAuth)
        {
            if (!await CanUserViewItem(parsedNamedQuery, controlFile))
            {
                return new(Stream.Null, PersistedProjectionStatus.Restricted, requiresAuth);
            }
        }

        var projection = await namedQueryStorageService.LoadProjection(parsedNamedQuery, cancellationToken);
        if (projection.Stream != null && projection.Stream != Stream.Null)
        {
            return new(projection.Stream!, PersistedProjectionStatus.Available, requiresAuth);
        }

        logger.LogWarning("File {S3Key} was successfully created but now cannot be loaded",
            parsedNamedQuery.StorageKey);
        return new(Stream.Null, PersistedProjectionStatus.Error, requiresAuth);
    }

    private async Task<StoredResult> TryGetExistingResource(StoredParsedNamedQuery parsedNamedQuery,
        bool validateRoles, CancellationToken cancellationToken)
    {
        var controlFile = await namedQueryStorageService.GetControlFile(parsedNamedQuery, cancellationToken);
        if (controlFile == null) return new(Stream.Null, PersistedProjectionStatus.NotFound, null);

        var itemKey = parsedNamedQuery.StorageKey;
        var requiresAuth = !controlFile.Roles.IsNullOrEmpty();
        
        if (validateRoles && requiresAuth)
        {
            if (!await CanUserViewItem(parsedNamedQuery, controlFile))
            {
                return new(Stream.Null, PersistedProjectionStatus.Restricted, true);
            }
        }

        if (controlFile.IsStale(namedQuerySettings.ControlStaleSecs)) // TODO - allow different values
        {
            logger.LogWarning("File {S3Key} has valid control-file but it is stale. Will recreate",
                itemKey);
            return new(Stream.Null, PersistedProjectionStatus.NotFound, requiresAuth);
        }

        if (controlFile.InProcess)
        {
            logger.LogWarning("File {S3Key} has valid control-file but it's in progress", itemKey);
            return new(Stream.Null, PersistedProjectionStatus.InProcess, requiresAuth);
        }

        var resource = await namedQueryStorageService.LoadProjection(parsedNamedQuery, cancellationToken);
        if (resource.Stream != null && resource.Stream != Stream.Null)
        {
            return new(resource.Stream!, PersistedProjectionStatus.Available, requiresAuth);
        }

        logger.LogWarning("File {S3Key} has valid control-file but item not found. Will recreate", itemKey);
        return new(Stream.Null, PersistedProjectionStatus.NotFound, requiresAuth);
    }

    private async Task<bool> CanUserViewItem(StoredParsedNamedQuery parsedNamedQuery, ControlFile controlFile)
    {
        var access =
            await assetAccessValidator.TryValidate(parsedNamedQuery.Customer, controlFile.Roles,
                AuthMechanism.Cookie);
        return access is AssetAccessResult.Open or AssetAccessResult.Authorized;
    }
}

public record StoredResult(Stream? Stream, PersistedProjectionStatus Status, bool? RequiresAuth);