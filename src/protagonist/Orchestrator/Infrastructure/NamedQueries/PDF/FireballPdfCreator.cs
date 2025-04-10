﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.Model.Assets;
using DLCS.Model.Assets.NamedQueries;
using DLCS.Web.Response;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Orchestrator.Infrastructure.NamedQueries.Persistence;
using Orchestrator.Infrastructure.NamedQueries.Persistence.Models;
using Orchestrator.Settings;

namespace Orchestrator.Infrastructure.NamedQueries.PDF;

/// <summary>
/// Use Fireball for projection of NamedQuery to PDF file
/// </summary>
/// <remarks>See https://github.com/fractos/fireball</remarks>
public class FireballPdfCreator : BaseProjectionCreator<PdfParsedNamedQuery>
{
    private const string PdfEndpoint = "pdf";
    private readonly IThumbSizeProvider thumbSizeProvider;
    private readonly HttpClient fireballClient;
    private readonly JsonSerializerSettings jsonSerializerSettings;

    public FireballPdfCreator(
        IBucketReader bucketReader,
        IBucketWriter bucketWriter,
        IOptions<NamedQuerySettings> namedQuerySettings,
        IThumbSizeProvider thumbSizeProvider,
        ILogger<FireballPdfCreator> logger,
        HttpClient fireballClient,
        IStorageKeyGenerator storageKeyGenerator
    ) : base(bucketReader, bucketWriter, namedQuerySettings, storageKeyGenerator, logger)
    {
        this.thumbSizeProvider = thumbSizeProvider;
        this.fireballClient = fireballClient;
        jsonSerializerSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };
    }

    protected override async Task<CreateProjectionResult> CreateFile(PdfParsedNamedQuery parsedNamedQuery,
        List<Asset> assets, CancellationToken cancellationToken)
    {
        var pdfKey = parsedNamedQuery.StorageKey;

        try
        {
            Logger.LogDebug("Creating new pdf document at {PdfS3Key}", pdfKey);
            var playbook = await GeneratePlaybook(pdfKey, parsedNamedQuery, assets);

            var fireballResponse = await CallFireball(cancellationToken, playbook, pdfKey);
            return fireballResponse;
        }
        catch (HttpRequestException ex)
        {
            Logger.LogError(ex, "Http exception calling fireball to generate PDF {PdfS3Key}", pdfKey);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unknown exception calling fireball to generate PDF {PdfS3Key}", pdfKey);
        }

        return new CreateProjectionResult();
    }

    private async Task<FireballPlaybook> GeneratePlaybook(string pdfKey, PdfParsedNamedQuery parsedNamedQuery,
        List<Asset> assets)
    {
        var playbook = new FireballPlaybook
        {
            Output = StorageKeyGenerator.GetOutputLocation(pdfKey).GetS3Uri().ToString(),
            Title = parsedNamedQuery.ObjectName,
            CustomTypes = new FireballCustomTypes
            {
                RedactedMessage = new FireballMessageProp { Message = parsedNamedQuery.RedactedMessage }
            }
        };
        
        var overrides = GetCustomerOverride(parsedNamedQuery);

        playbook.Pages.Add(FireballPage.Download(parsedNamedQuery.CoverPageUrl));

        int pageNumber = 0;
        foreach (var i in NamedQueryProjections.GetOrderedAssets(assets, parsedNamedQuery))
        {
            Logger.LogTrace("Adding PDF page {PdfPage} to {PdfS3Key} for {Image}", pageNumber++, pdfKey, i.Id);
            if (i.RequiresAuth && !RolesAreOnWhitelist(i, overrides))
            {
                Logger.LogDebug("Image {Image} on page {PdfPage} of {PdfS3Key} requires auth, redacting", i.Id,
                    pageNumber++, pdfKey);
                playbook.Pages.Add(FireballPage.Redacted());
            }
            else
            {
                var thumbToInclude = await GetThumbnailLocation(i);
                if (thumbToInclude != null)
                {
                    playbook.Pages.Add(FireballPage.Image(thumbToInclude.GetS3Uri().ToString()));
                }
            }
        }

        return playbook;
    }

    private async Task<ObjectInBucket?> GetThumbnailLocation(Asset asset)
    {
        var availableSizes = await thumbSizeProvider.GetThumbSizesForImage(asset);

        if (availableSizes.IsEmpty())
        {
            Logger.LogInformation("Unable to find thumbnail for {AssetId}, excluding from PDF", asset.Id);
            return null;
        }
        
        var selectedSize = availableSizes.SizeClosestTo(NamedQuerySettings.ProjectionThumbsize, out var isOpen);
        Logger.LogTrace("Using thumbnail {ThumbnailSize} for asset {AssetId}. IsOpen: {ThumbnailOpen}", selectedSize,
            asset.Id, isOpen);
        return StorageKeyGenerator.GetThumbnailLocation(asset.Id, selectedSize.MaxDimension, isOpen);
    }

    private CustomerOverride GetCustomerOverride(PdfParsedNamedQuery parsedNamedQuery) 
        => NamedQuerySettings.CustomerOverrides.TryGetValue(parsedNamedQuery.Customer.ToString(), out var overrides)
            ? overrides
            : new CustomerOverride();
    
    private static bool RolesAreOnWhitelist(Asset i, CustomerOverride overrides) 
        => i.RolesList.All(r => overrides.PdfRolesWhitelist.Contains(r));

    private async Task<CreateProjectionResult?> CallFireball(CancellationToken cancellationToken, FireballPlaybook playbook, string pdfKey)
    {
        var jsonString = JsonConvert.SerializeObject(playbook, jsonSerializerSettings);
        var request = new HttpRequestMessage(HttpMethod.Post, PdfEndpoint)
        {
            Content = new StringContent(jsonString, Encoding.UTF8, "application/json")
        };
        Logger.LogDebug("Calling fireball to create new pdf document at {PdfS3Key}", pdfKey);
        var sw = Stopwatch.StartNew();
        var response = await fireballClient.SendAsync(request, cancellationToken);
        var fireballResponse = await response.ReadAsJsonAsync<CreateProjectionResult>(true, jsonSerializerSettings);
        sw.Stop();
        Logger.LogDebug("Created new pdf document at {PdfS3Key} with size in bytes = {SizeBytes}. Took {Elapsed}ms",
            pdfKey, fireballResponse?.Size ?? -1, sw.ElapsedMilliseconds);
        return fireballResponse;
    }
}

public class FireballPlaybook
{
    public string Method { get; set; } = "s3";
    
    public string Output { get; set; }
    
    public string Title { get; set; }
    
    public FireballCustomTypes CustomTypes { get; set; }

    public List<FireballPage> Pages { get; set; } = new();
}

public class FireballCustomTypes
{
    [JsonProperty("redacted")] 
    public FireballMessageProp RedactedMessage { get; set; }

    [JsonProperty("missing")] 
    public FireballMessageProp MissingMessage { get; set; } = new() { Message = "Unable to display this page" };
}

public class FireballMessageProp
{
    public string Message { get; set; }
}

public class FireballPage
{
    public string Type { get; set; }
    
    public string? Method { get; set; }
    
    public string? Input { get; set; }

    public static FireballPage Redacted() => new() { Type = "redacted" };

    public static FireballPage Download(string url) =>
        new() { Type = "pdf", Method = "download", Input = url };
    
    public static FireballPage Image(string url) =>
        new() { Type = "jpg", Method = "s3", Input = url };
}