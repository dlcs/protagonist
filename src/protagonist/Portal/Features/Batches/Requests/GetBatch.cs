using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using API.Client;
using DLCS.Core.Settings;
using DLCS.HydraModel;
using DLCS.Web.Auth;
using Hydra.Collections;
using MediatR;
using Microsoft.Extensions.Options;

namespace Portal.Features.Batches.Requests;

public class GetBatch : IRequest<GetBatchResult>
{
    public int BatchId { get; set; }
}

public class GetBatchResult
{
    public Batch Batch { get; set; }
    public HydraCollection<Image> Images { get; set; }
    public Dictionary<string, string> Thumbnails { get; set; }
}

public class GetBatchHandler : IRequestHandler<GetBatch, GetBatchResult>
{
    private readonly DlcsSettings dlcsSettings;
    private readonly IDlcsClient dlcsClient;
    private readonly string customerId;
    
    public GetBatchHandler(IDlcsClient dlcsClient, ClaimsPrincipal currentUser, IOptions<DlcsSettings> dlcsSettings)
    {
        this.dlcsClient = dlcsClient;
        this.dlcsSettings = dlcsSettings.Value;
        customerId = (currentUser.GetCustomerId() ?? -1).ToString();
    }

    public async Task<GetBatchResult> Handle(GetBatch request, CancellationToken cancellationToken)
    {
        var batch = await dlcsClient.GetBatch(request.BatchId);
        var images = await dlcsClient.GetBatchImages(request.BatchId);
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