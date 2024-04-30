using System;
using System.Collections.Generic;
using System.Threading;
using DLCS.Model.Assets;
using DLCS.Model.Assets.NamedQueries;
using DLCS.Repository.NamedQueries.Models;
using Orchestrator.Infrastructure.NamedQueries.Persistence;

namespace Orchestrator.Tests.Integration.Infrastructure;

/// <summary>
/// Fake projection creator that handles configured callbacks for when ParsedNamedQuery is persisted.
/// Also optional callback for when ControlFile is created during persistence.
/// </summary>
public class FakePdfCreator : IProjectionCreator<PdfParsedNamedQuery>
{
    private static readonly Dictionary<string, Func<ParsedNamedQuery, List<Asset>, bool>> Callbacks = new();

    private static readonly Dictionary<string, Func<ControlFile, ControlFile>> ControlFileCallbacks = new();

    public void AddCallbackFor(string pdfKey, Func<ParsedNamedQuery, List<Asset>, bool> callback)
        => Callbacks.Add(pdfKey, callback);

    public void AddCallbackFor(string pdfKey, Func<ControlFile, ControlFile> callback)
        => ControlFileCallbacks.Add(pdfKey, callback);

    public Task<(bool success, ControlFile controlFile)> PersistProjection(PdfParsedNamedQuery parsedNamedQuery,
        List<Asset> images, CancellationToken cancellationToken = default)
    {
        if (Callbacks.TryGetValue(parsedNamedQuery.StorageKey, out var cb))
        {
            var controlFileCallback = ControlFileCallbacks.TryGetValue(parsedNamedQuery.StorageKey, out var cfcb)
                ? cfcb
                : file => file;

            return Task.FromResult((cb(parsedNamedQuery, images), controlFileCallback(new ControlFile())));
        }

        throw new Exception($"Request with key {parsedNamedQuery.StorageKey} not setup");
    }
}