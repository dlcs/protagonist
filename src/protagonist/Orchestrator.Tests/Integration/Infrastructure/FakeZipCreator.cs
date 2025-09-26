using System;
using System.Collections.Generic;
using System.Threading;
using DLCS.Model.Assets;
using DLCS.Model.Assets.NamedQueries;
using DLCS.Repository.NamedQueries.Models;
using Orchestrator.Infrastructure.NamedQueries.Persistence;

namespace Orchestrator.Tests.Integration.Infrastructure;

public class FakeZipCreator : IProjectionCreator<ZipParsedNamedQuery>
{
    private static readonly Dictionary<string, Func<ParsedNamedQuery, List<Asset>, bool>> callbacks = new();
    
    /// <summary>
    /// Add a callback for when zip is to be created and persisted to S3, allows control of success/failure for
    /// testing
    /// </summary>
    public void AddCallbackFor(string s3Key, Func<ParsedNamedQuery, List<Asset>, bool> callback)
        => callbacks.Add(s3Key, callback);

    public Task<(bool success, ControlFile controlFile)> PersistProjection(ZipParsedNamedQuery parsedNamedQuery, List<Asset> images,
        CancellationToken cancellationToken = default)
    {
        if (callbacks.TryGetValue(parsedNamedQuery.StorageKey, out var cb))
        {
            return Task.FromResult((cb(parsedNamedQuery, images), new ControlFile()));
        }

        throw new Exception($"Request with key {parsedNamedQuery.StorageKey} not setup");
    }

    public Task MarkControlFileComplete(ZipParsedNamedQuery parsedNamedQuery, ControlFile controlFile, long fileSize)
    {
        return Task.CompletedTask;
    }
}
