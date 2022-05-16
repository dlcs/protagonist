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
    public class PutImage : IRequest<Asset>
    {
        public Asset Asset { get; set; }
        
        public PutImage(Asset asset)
        {
            this.Asset = asset;
        }
    }

    public class PutImageHandler : IRequestHandler<PutImage, Asset>
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

        public async Task<Asset> Handle(PutImage request, CancellationToken cancellationToken)
        {
            var putAsset = request.Asset;
            // DELIVERATOR: https://github.com/digirati-co-uk/deliverator/blob/master/API/Architecture/Request/API/Entities/CustomerSpaceImage.cs#L74
            
            // We are going to need to have cached versions of all these policies, but just memoryCache for API I think
            
            // Deliverator Logic:
            // LoadSpaceBehaviour - need the space to exist (space cache?)
            var targetSpace = await spaceRepository.GetSpace(putAsset.Customer, putAsset.Space, cancellationToken);
            if (targetSpace == null)
            {
                throw new BadRequestException(
                    $"Target space for asset PUT does not exist: {putAsset.Customer}/{putAsset.Space}");
            }
            
            // LoadImageBehaviour - see if already exists
            var existingAsset = await assetRepository.GetAsset(putAsset.Id);
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
                    throw new BadRequestException(
                        $"Cannot add more assets: maximum is {counts.MaximumNumberOfStoredImages}");
                }
                
            }

            var validationResult = AssetValidator.ValidateImageUpsert(existingAsset, putAsset);
            if (!validationResult.Success)
            {
                // ValidateImageUpsertBehaviour
                throw new BadRequestException(validationResult.ErrorMessage);
            }

            if (existingAsset == null)
            {
                await SelectImageOptimisationPolicy(putAsset);
                await SelectThumbnailPolicy(putAsset);
            }

            await assetRepository.Put(putAsset);

            // UpdateImageBehaviour - store in DB
            // CreateSkeletonImageLocationBehaviour
            // UpdateImageLocationBehaviour
            // if Image:
            // CallImageIngestEndpointBehaviour
            // LoadImageBehaviour (reload)
            // return 200 or 201 or 500
            // else

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