using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using API.Client;
using DLCS.HydraModel;
using IIIF.ImageApi.V3;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Portal.Features.Spaces.Requests;

public class GetImage : IRequest<GetImageResult?>
{
    public int SpaceId { get; set; }
    public string ImageId { get; set; }
}

public class GetImageResult
{
    public Image Image { get; set; }
    public ImageService3? ImageThumbnailService { get; set; }
    public ImageStorage? ImageStorage { get; set; }
}

public class GetImageHandler : IRequestHandler<GetImage, GetImageResult?>
{
    private readonly ILogger<DlcsClient> logger;
    private readonly IDlcsClient dlcsClient;
    private readonly HttpClient httpClient;

    public GetImageHandler(IDlcsClient dlcsClient, HttpClient httpClient, ILogger<DlcsClient> logger)
    {
        this.logger = logger;
        this.dlcsClient = dlcsClient;
        this.httpClient = httpClient;
    }
    
    public async Task<GetImageResult?> Handle(GetImage request, CancellationToken cancellationToken)
    {
        var image = await dlcsClient.GetImage(request.SpaceId, request.ImageId);
        if (image == null)
        {
            return null;
        }
        
        var imageStorage = await GetImageStorage(image);
        var thumbnailService = await GetImageThumbnailService(image);
        return new GetImageResult() { 
            Image = image, 
            ImageThumbnailService = thumbnailService,
            ImageStorage = imageStorage
        };
    }
    
    private async Task<ImageService3?> GetImageThumbnailService(Image image)
    {
        try
        {
            var response = await httpClient.GetAsync($"{image.ThumbnailImageService}/info.json");
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<ImageService3>();
        }
        catch (Exception ex) 
        {  
            logger.LogError("Failed to deserialize thumbnail image service {ImageThumbnailService}", image.ThumbnailImageService);
            return null;
        }
    }

    private async Task<ImageStorage?> GetImageStorage(Image image)
    {
        try
        {
            return await dlcsClient.GetImageStorage(image.Space, image.ModelId);
        }
        catch (Exception ex) 
        {  
            logger.LogError("Failed to deserialize image storage {ImageStorageService}", image.Storage);
            return null;
        }
    }
}