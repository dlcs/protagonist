using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Settings;
using DLCS.Core.Strings;
using DLCS.Model.Assets;
using DLCS.Model.Messaging;
using DLCS.Model.Spaces;
using DLCS.Model.Storage;
using MediatR;
using Microsoft.Extensions.Options;

namespace API.Features.Image.Requests
{
    /// <summary>
    /// Unlike Deliverator we will handle PUTs and PATCHes with a single command, with
    /// slight variation in behaviour.
    /// </summary>
    public class PutOrPatchImage : IRequest<PutOrPatchImageResult>
    {
        /// <summary>
        /// The Asset to be updated or inserted; may contain null fields indicating no change
        /// </summary>
        public Asset? Asset { get; set; }

        /// <summary>
        /// PUT or POST
        /// (While this leaks HTTP into the Mediatr it's clearer than using Create or Update) 
        /// </summary>
        public string? Method { get; set; }
    }

    /// <summary>
    /// The outcome of an update or insert operation
    /// </summary>
    public class PutOrPatchImageResult
    {
        /// <summary>
        /// The asset, which may have been processed by Engine during this operation
        /// </summary>
        public Asset? Asset { get; set; }
        
        /// <summary>
        /// This leaks HTTP into the Mediatr... If a trip to Engine was involved,
        /// this will be the eventual status code returned by the synchronous Engine call.
        /// </summary>
        public HttpStatusCode? StatusCode { get; set; }
        
        /// <summary>
        /// An error message, if anything went wrong.
        /// </summary>
        public string? Message { get; set; }
    }

    /// <summary>
    /// 
    /// </summary>
    public class PutOrPatchImageHandler : IRequestHandler<PutOrPatchImage, PutOrPatchImageResult>
    {
        private readonly ISpaceRepository spaceRepository;
        private readonly IAssetRepository assetRepository;
        private readonly IStorageRepository storageRepository;
        private readonly IThumbnailPolicyRepository thumbnailPolicyRepository;
        private readonly IImageOptimisationPolicyRepository imageOptimisationPolicyRepository;
        private readonly IMessageBus messageBus;
        private readonly DlcsSettings settings;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="spaceRepository"></param>
        /// <param name="assetRepository"></param>
        /// <param name="storageRepository"></param>
        /// <param name="thumbnailPolicyRepository"></param>
        /// <param name="imageOptimisationPolicyRepository"></param>
        /// <param name="messageBus"></param>
        /// <param name="dlcsSettings"></param>
        public PutOrPatchImageHandler(
            ISpaceRepository spaceRepository,
            IAssetRepository assetRepository,
            IStorageRepository storageRepository,
            IThumbnailPolicyRepository thumbnailPolicyRepository,
            IImageOptimisationPolicyRepository imageOptimisationPolicyRepository,
            IMessageBus messageBus,
            IOptions<DlcsSettings> dlcsSettings)
        {
            this.spaceRepository = spaceRepository;
            this.assetRepository = assetRepository;
            this.storageRepository = storageRepository;
            this.thumbnailPolicyRepository = thumbnailPolicyRepository;
            this.imageOptimisationPolicyRepository = imageOptimisationPolicyRepository;
            this.messageBus = messageBus;
            this.settings = dlcsSettings.Value;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="request"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<PutOrPatchImageResult> Handle(PutOrPatchImage request, CancellationToken cancellationToken)
        {
            var asset = request.Asset;
            if (asset == null || request.Method == null)
            {
                return new PutOrPatchImageResult
                {
                    StatusCode = HttpStatusCode.BadRequest,
                    Message = "Invalid Request"
                };
            }
            
            if (request.Method is not ("PUT" or "PATCH"))
            {
                return new PutOrPatchImageResult
                {
                    StatusCode = HttpStatusCode.MethodNotAllowed,
                    Message = "Method must be PUT or PATCH"
                };
            }
            
            // TEMPORARY happy path just for images
            if (asset.Family > 0 && asset.Family != AssetFamily.Image)
            {
                return new PutOrPatchImageResult
                {
                    StatusCode = HttpStatusCode.BadRequest,
                    Message = "Just images for the moment!!!"
                };
            }
            
            // DELIVERATOR: https://github.com/digirati-co-uk/deliverator/blob/master/API/Architecture/Request/API/Entities/CustomerSpaceImage.cs#L74
            // We are going to need to have cached versions of all these policies, but just memoryCache for API I think
            // appCache inside repositories.
            
            // Deliverator Logic:
            // LoadSpaceBehaviour - need the space to exist: allow this to be cached
            var targetSpace = await spaceRepository.GetSpace(asset.Customer, asset.Space, cancellationToken, noCache:false);
            if (targetSpace == null)
            {
                return new PutOrPatchImageResult
                {
                    StatusCode = HttpStatusCode.BadRequest,
                    Message = $"Target space for asset does not exist: {asset.Customer}/{asset.Space}"
                };
            }
            
            // LoadImageBehaviour - see if already exists
            Asset? existingAsset;
            try
            {
                existingAsset = await assetRepository.GetAsset(asset.Id, noCache:true);
                if (existingAsset == null)
                {
                    if (request.Method == "PATCH")
                    {
                        return new PutOrPatchImageResult
                        {
                            StatusCode = HttpStatusCode.NotFound,
                            Message = "Attempted to PATCH an Asset that could not be found."
                        };
                    }
                    // LoadCustomerStorageBehaviour - if a new image, CustomerStorageCalculation
                    // LoadStoragePolicyBehaviour - get the storage policy
                    // EnforceStoragePolicyForNumberOfImagesBehaviour
                    // This is a temporary simple solution
                    // will use this policy, somewhere... go back through deliverator.
                    var storagePolicy = settings.IngestDefaults.StoragePolicy;
                    var counts = await storageRepository.GetImageCounts(asset.Customer);
                    if (counts.CurrentNumberOfStoredImages >= counts.MaximumNumberOfStoredImages)
                    {
                        return new PutOrPatchImageResult
                        {
                            StatusCode = HttpStatusCode.InsufficientStorage,
                            Message = $"This operation will fall outside of your storage policy for number of images: maximum is {counts.MaximumNumberOfStoredImages}"
                        };
                    }
                }
            }
            catch (Exception e)
            {
                return new PutOrPatchImageResult
                {
                    StatusCode = HttpStatusCode.InternalServerError,
                    Message = e.Message
                };
            }

            // TEMPORARY! Prevent PUT upserts of non-images for now, too.
            if (existingAsset != null && existingAsset.Family != AssetFamily.Image)
            {
                return new PutOrPatchImageResult
                {
                    StatusCode = HttpStatusCode.BadRequest,
                    Message = "Just images for the moment!!!"
                };
            }
            
            // In the deliverator flow, the equivalent of this came after the validation below.
            // But it seems better to do it before.
            if (existingAsset == null)
            {
                await SelectImageOptimisationPolicy(asset);
                await SelectThumbnailPolicy(asset);
            }
            
            var validationResult = AssetPreparer.PrepareAssetForUpsert(
                existingAsset, asset, allowNonApiUpdates:false);
            if (!validationResult.Success)
            {
                // ValidateImageUpsertBehaviour
                return new PutOrPatchImageResult
                {
                    StatusCode = HttpStatusCode.BadRequest,
                    Message = validationResult.ErrorMessage
                };
            }

            // (SelectImageOptimisationPolicy, SelectThumbnailPolicy was done after validation in Deliverator)

            // UpdateImageBehaviour - store in DB
            await assetRepository.Save(asset, cancellationToken);

            // now obtain the asset again
            var assetAfterSave = await assetRepository.GetAsset(asset.Id, noCache: true);
            if (assetAfterSave == null)
            {
                return new PutOrPatchImageResult
                {
                    StatusCode = HttpStatusCode.InternalServerError,
                    Message = validationResult.ErrorMessage
                };
            }
            
            // TODO: This is incomplete, it only deals with images for now
            // needs the equivalent  Conditions.ControlStateNotEquals(name: "image.family", value: "I")
            //                                              ^^^
            if (asset.Family == AssetFamily.Image)
            {
                await messageBus.SendAssetModifiedNotification(asset, assetAfterSave);
                if (validationResult.RequiresReingest || request.Method == "PUT")
                {
                    // we treat a PUT as a re-process
                    // await call to engine load-balancer, which processes synchronously (not a queue)
                    var ingestAssetRequest = new IngestAssetRequest(assetAfterSave, DateTime.UtcNow);
                    var statusCode = await messageBus.SendImmediateIngestAssetRequest(ingestAssetRequest, false);
                    if (statusCode is HttpStatusCode.Created or HttpStatusCode.OK)
                    {
                        // obtain it again after Engine has processed it
                        var assetAfterEngine = await assetRepository.GetAsset(asset.Id, noCache:true);
                        return new PutOrPatchImageResult
                        {
                            Asset = assetAfterEngine,
                            StatusCode = statusCode
                        };
                    }

                    return new PutOrPatchImageResult
                    {
                        Asset = assetAfterSave,
                        Message = "Engine was not able to process this asset",
                        StatusCode = statusCode
                    };
                }

                return new PutOrPatchImageResult
                {
                    Asset = assetAfterSave,
                    StatusCode = existingAsset == null ? HttpStatusCode.Created : HttpStatusCode.OK
                };
            }
            // 
            // LoadImageBehaviour (reload)
            // return 200 or 201 or 500
            // else
            return new PutOrPatchImageResult
            {
                StatusCode = HttpStatusCode.NotImplemented,
                Message = "This handler has a limited implementation for now."
            };
        }

        private async Task SelectThumbnailPolicy(Asset asset)
        {
            if (asset.Family == AssetFamily.Image)
            {
                await SetThumbnailPolicy(settings.IngestDefaults.ThumbnailPolicies.Graphics, asset);
            }
            else if (asset.Family == AssetFamily.Timebased && asset.MediaType.HasText() && asset.MediaType.Contains("video/"))
            {
                await SetThumbnailPolicy(settings.IngestDefaults.ThumbnailPolicies.Video, asset);
            }
        }

        private async Task SelectImageOptimisationPolicy(Asset asset)
        {
            if (asset.Family == AssetFamily.Image)
            {
                await SetImagePolicy(settings.IngestDefaults.ImageOptimisationPolicies.Graphics, asset);
            }
            else if (asset.Family == AssetFamily.Timebased && asset.MediaType.HasText())
            {
                if (asset.MediaType.Contains("video/"))
                {
                    await SetImagePolicy(settings.IngestDefaults.ImageOptimisationPolicies.Video, asset);
                }
                else if (asset.MediaType.Contains("audio/"))
                {
                    await SetImagePolicy(settings.IngestDefaults.ImageOptimisationPolicies.Audio, asset);
                }
            }
        }

        private async Task SetImagePolicy(string key, Asset asset)
        {
            // This is adapted from Deliverator, but there doesn't seem to be a way of 
            // taking the policy from the incoming PUT
            var imagePolicy = await imageOptimisationPolicyRepository.GetImageOptimisationPolicy(key);
            if (imagePolicy != null)
            {
                asset.ImageOptimisationPolicy = imagePolicy.Id;
            }
        }
        
        private async Task SetThumbnailPolicy(string key, Asset asset)
        {
            var thumbnailPolicy = await thumbnailPolicyRepository.GetThumbnailPolicy(key);
            if (thumbnailPolicy != null)
            {
                asset.ThumbnailPolicy = thumbnailPolicy.Id;
            }
        }
    }
}