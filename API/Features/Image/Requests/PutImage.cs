using System.Threading;
using System.Threading.Tasks;
using DLCS.Model.Assets;
using DLCS.Model.Messaging;
using DLCS.Model.Spaces;
using DLCS.Model.Storage;
using MediatR;

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
        private readonly IMessageBus messageBus;

        public PutImageHandler(
            ISpaceRepository spaceRepository,
            IAssetRepository assetRepository,
            IStorageRepository storageRepository,
            IMessageBus messageBus)
        {
            this.spaceRepository = spaceRepository;
            this.assetRepository = assetRepository;
            this.storageRepository = storageRepository;
            this.messageBus = messageBus;
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
            if (existingAsset != null)
            {
                // LoadCustomerStorageBehaviour - if a new image, CustomerStorageCalculation
                // LoadStoragePolicyBehaviour - get the storage policy
                // EnforceStoragePolicyForNumberOfImagesBehaviour
                
                
            }
            // ValidateImageUpsertBehaviour
            // Load default ImageOptimisationPolicies and ThumbnailPolicies
            // LoadAllImageOptimisationPoliciesBehaviour
            // LoadAllThumbnailPoliciesBehaviour
            // SelectImageOptimisationPolicyForImageBehaviour - whether image, audio or video
            // SelectThumbnailPolicyForImageBehaviour - image, audio or video
            // UpdateImageBehaviour - store in DB
            // CreateSkeletonImageLocationBehaviour
            // UpdateImageLocationBehaviour
            // if Image:
                // CallImageIngestEndpointBehaviour
                // LoadImageBehaviour (reload)
                // return 200 or 201 or 500
            // else

        }
    }
}