using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Settings;
using DLCS.Model.Assets;
using DLCS.Model.Messaging;
using DLCS.Model.Spaces;
using DLCS.Model.Storage;
using MediatR;
using Microsoft.Extensions.Options;

namespace API.Features.Image.Requests
{
    // maybe name this `PutAsset` ?
    public class PutImage : IRequest<PutImageResult>
    {
        public Asset Asset { get; set; }
        
        public PutImage(Asset asset)
        {
            this.Asset = asset;
        }
    }

    public class PutImageResult
    {
        public Asset? Asset { get; set; }
        public HttpStatusCode? StatusCode { get; set; }
        public string? Message { get; set; }
    }

    public class PutImageHandler : IRequestHandler<PutImage, PutImageResult>
    {
        private ISpaceRepository spaceRepository;
        private IAssetRepository assetRepository;
        private IStorageRepository storageRepository;
        private IThumbnailPolicyRepository thumbnailPolicyRepository;
        private IImageOptimisationPolicyRepository imageOptimisationPolicyRepository;
        private readonly IMessageBus messageBus;
        private DlcsSettings settings;

        public PutImageHandler(
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

        public async Task<PutImageResult> Handle(PutImage request, CancellationToken cancellationToken)
        {
            var putAsset = request.Asset;

            // Need to set defaults (see convo with Donald) before we get to the DB
            // And then have subsequent tests for null PUTs not breaking defaults.
            // if not an existing asset, should set defaults from BEAST.
            
            // temporary happy path just for images
            if (putAsset.Family > 0 && putAsset.Family != AssetFamily.Image)
            {
                return new PutImageResult
                {
                    StatusCode = HttpStatusCode.BadRequest,
                    Message = "Just images for the moment!!!"
                };
            }
            
            // DELIVERATOR: https://github.com/digirati-co-uk/deliverator/blob/master/API/Architecture/Request/API/Entities/CustomerSpaceImage.cs#L74
            // We are going to need to have cached versions of all these policies, but just memoryCache for API I think
            // appCache inside repositories.
            
            // Deliverator Logic:
            // LoadSpaceBehaviour - need the space to exist (space cache?)
            var targetSpace = await spaceRepository.GetSpace(putAsset.Customer, putAsset.Space, cancellationToken);
            if (targetSpace == null)
            {
                return new PutImageResult
                {
                    StatusCode = HttpStatusCode.BadRequest,
                    Message = $"Target space for asset PUT does not exist: {putAsset.Customer}/{putAsset.Space}"
                };
            }
            
            // LoadImageBehaviour - see if already exists
            Asset? existingAsset;
            try
            {
                existingAsset = await assetRepository.GetAsset(putAsset.Id);
                if (existingAsset == null)
                {
                    // LoadCustomerStorageBehaviour - if a new image, CustomerStorageCalculation
                    // LoadStoragePolicyBehaviour - get the storage policy
                    // EnforceStoragePolicyForNumberOfImagesBehaviour
                    // This is a temporary simple solution
                    // will use this policy, somewhere... go back through deliverator.
                    var storagePolicy = settings.IngestDefaults.StoragePolicy;
                    var counts = await storageRepository.GetImageCounts(putAsset.Customer);
                    if (counts.CurrentNumberOfStoredImages >= counts.MaximumNumberOfStoredImages)
                    {
                        return new PutImageResult
                        {
                            StatusCode = HttpStatusCode.InsufficientStorage,
                            Message = $"This operation will fall outside of your storage policy for number of images: maximum is {counts.MaximumNumberOfStoredImages}"
                        };
                    }
                }
            }
            catch (Exception e)
            {
                return new PutImageResult
                {
                    StatusCode = HttpStatusCode.InternalServerError,
                    Message = e.Message
                };
            }

            // Prevent PUT upserts of non-images for now, too.
            if (existingAsset != null && existingAsset.Family != AssetFamily.Image)
            {
                return new PutImageResult
                {
                    StatusCode = HttpStatusCode.BadRequest,
                    Message = "Just images for the moment!!!"
                };
            }

            var validationResult = AssetValidator.ValidateImageUpsert(existingAsset, putAsset);
            if (!validationResult.Success)
            {
                // ValidateImageUpsertBehaviour
                return new PutImageResult
                {
                    StatusCode = HttpStatusCode.BadRequest,
                    Message = validationResult.ErrorMessage
                };
            }

            if (existingAsset == null)
            {
                await SelectImageOptimisationPolicy(putAsset);
                await SelectThumbnailPolicy(putAsset);
            }

            // UpdateImageBehaviour - store in DB
            await assetRepository.Put(putAsset, cancellationToken, "PUT");

            // TODO: This is incomplete, it only deals with images for now
            // needs the equivalent  Conditions.ControlStateNotEquals(name: "image.family", value: "I")
            //                                              ^^^
            if (putAsset.Family == AssetFamily.Image)
            {
                // synchronous call to engine load-balancer
                var ingestAssetRequest = new IngestAssetRequest(putAsset, DateTime.UtcNow);
                var statusCode = await messageBus.SendImmediateIngestAssetRequest(ingestAssetRequest, false);
                // The above is a synchronous call, engine will process and return
                if (statusCode == HttpStatusCode.Created || statusCode == HttpStatusCode.OK)
                {
                    var dbAsset = await assetRepository.GetAsset(putAsset.Id);
                    return new PutImageResult
                    {
                        Asset = dbAsset,
                        StatusCode = statusCode
                    };
                }
            }
            // 
            // LoadImageBehaviour (reload)
            // return 200 or 201 or 500
            // else
            return new PutImageResult
            {
                StatusCode = HttpStatusCode.NotImplemented,
                Message = "This handler has a limited implementation for now."
            };
        }

        private async Task SelectThumbnailPolicy(Asset putAsset)
        {
            if (putAsset.Family == AssetFamily.Image)
            {
                await SetThumbnailPolicy(settings.IngestDefaults.ThumbnailPolicies.Graphics, putAsset);
            }
            else if (putAsset.Family == AssetFamily.Timebased && putAsset.MediaType.Contains("video/"))
            {
                await SetThumbnailPolicy(settings.IngestDefaults.ThumbnailPolicies.Video, putAsset);
            }
        }

        private async Task SelectImageOptimisationPolicy(Asset putAsset)
        {
            if (putAsset.Family == AssetFamily.Image)
            {
                await SetImagePolicy(settings.IngestDefaults.ImageOptimisationPolicies.Graphics, putAsset);
            }
            else if (putAsset.Family == AssetFamily.Timebased)
            {
                if (putAsset.MediaType.Contains("video/"))
                {
                    await SetImagePolicy(settings.IngestDefaults.ImageOptimisationPolicies.Video, putAsset);
                }
                else if (putAsset.MediaType.Contains("audio/"))
                {
                    await SetImagePolicy(settings.IngestDefaults.ImageOptimisationPolicies.Audio, putAsset);
                }
            }
        }

        private async Task SetImagePolicy(string key, Asset putAsset)
        {
            // This is adapted from Deliverator, but there doesn't seem to be a way of 
            // taking the policy from the incoming PUT
            var imagePolicy = await imageOptimisationPolicyRepository.GetImageOptimisationPolicy(key);
            if (imagePolicy != null)
            {
                putAsset.ImageOptimisationPolicy = imagePolicy.Id;
            }
        }
        
        private async Task SetThumbnailPolicy(string key, Asset putAsset)
        {
            var thumbnailPolicy = await thumbnailPolicyRepository.GetThumbnailPolicy(key);
            if (thumbnailPolicy != null)
            {
                putAsset.ThumbnailPolicy = thumbnailPolicy.Id;
            }
        }
    }
}