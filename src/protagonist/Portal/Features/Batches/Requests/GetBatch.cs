using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using API.Client;
using DLCS.Core.Settings;
using DLCS.HydraModel;
using DLCS.Web.Auth;
using Hydra.Collections;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Portal.Features.Batches.Requests;

public class GetBatch : IRequest<GetBatchResult?>
{
    public int BatchId { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class GetBatchResult
{
    public Batch Batch { get; set; }
    public HydraCollection<Image> Images { get; set; }
    public Dictionary<string, string> Thumbnails { get; set; }
}

public class GetBatchHandler : IRequestHandler<GetBatch, GetBatchResult?>
{
    private readonly DlcsSettings dlcsSettings;
    private readonly IDlcsClient dlcsClient;
    private readonly ILogger<GetBatchHandler> logger;
    private readonly string customerId;
    
    public GetBatchHandler(
        IDlcsClient dlcsClient, 
        ClaimsPrincipal currentUser, 
        IOptions<DlcsSettings> dlcsSettings, 
        ILogger<GetBatchHandler> logger)
    {
        this.dlcsClient = dlcsClient;
        this.logger = logger;
        this.dlcsSettings = dlcsSettings.Value;
        customerId = (currentUser.GetCustomerId() ?? -1).ToString();
    }

    public async Task<GetBatchResult?> Handle(GetBatch request, CancellationToken cancellationToken)
    {
        Batch batch;
        try
        {
            batch = await dlcsClient.GetBatch(request.BatchId);
        }
        catch(DlcsException ex)
        {
            logger.LogError(ex, "Failed to retrieve batch {CustomerId}/queue/batches/{BatchId} from API",
                customerId, request.BatchId);
            return null;
        }
        
        var images = await dlcsClient.GetBatchImages(request.BatchId, request.Page, request.PageSize);
        var thumbnails = new Dictionary<string, string>();
        
        foreach (var image in images.Members)
        {
            var thumbnailSrc = new Uri(dlcsSettings.ResourceRoot, 
                $"thumbs/{customerId}/{image.Space}/{image.ModelId}/full/full/0/default.jpg").ToString();
            thumbnails.Add(image.ModelId, thumbnailSrc);
        }
        
        return new GetBatchResult() {
            Batch = batch,
            Images = images,
            Thumbnails = thumbnails
        };
    }
}