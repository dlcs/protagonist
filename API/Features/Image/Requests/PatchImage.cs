using System;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Strings;
using DLCS.Model.Assets;
using DLCS.Model.Messaging;
using MediatR;

namespace API.Features.Image.Requests
{
    public class PatchImage : IRequest<Asset>
    {
        public Asset Asset { get; set; }
        public int CustomerId { get; set; }
        public int SpaceId { get; set; }
        
        public PatchImage(int customerId, int spaceId, Asset asset)
        {
            CustomerId = customerId; // CustomerId must be the same as asset.customerId
            SpaceId = spaceId; // For now, spaceId must be the same - because it's part of the DB key.
            Asset = asset;
        }
    }

    public class PatchImageHandler : IRequestHandler<PatchImage, Asset>
    {
        private readonly IAssetRepository assetRepository;
        private readonly IMessageBus messageBus;

        public PatchImageHandler(
            IAssetRepository assetRepository,
            IMessageBus messageBus)
        {
            this.assetRepository = assetRepository;
            this.messageBus = messageBus;
        }

        public async Task<Asset> Handle(PatchImage request, CancellationToken cancellationToken)
        {
            // Deliverator version throws exception if you try to change customer, space, family.
            // This currently ignores those (not patchable but won't error)
            var patch = request.Asset;
            
            // can't pass cancellationToken here; this is currently a Dapper repository.
            var dbImage = await assetRepository.GetAsset(patch.Id);
            if (dbImage == null)
            {
                var apiEx = new APIException("Asset to be patched is unknown to DLCS")
                {
                    Label = "No asset for key " + request.Asset.Id, StatusCode = 400
                };
                throw apiEx;
            }

            // Validate the asset's ID
            if (dbImage.Customer != request.CustomerId || dbImage.Space != request.SpaceId)
            {
                var apiEx = new APIException("Asset to be patched has different Customer or Space from request")
                {
                    Label = $"Request: {request.CustomerId}/{request.SpaceId}, DB: {dbImage.Customer}/{dbImage.Space}",
                    StatusCode = 400
                };
                throw apiEx;
            }
            
            var before = dbImage.ShallowCopy();
            bool requiresReingest = false;

            // DISCUSS: Should the DLCS metadata DB cols BE NULLABLE?
            // The below allows you to omit a value and skip patching.
            // We need to be very clear what an incoming null value means. The below doesn't allow you to set 
            // a value to null, to remove it from the API response.
            // Deliverator version: https://github.com/digirati-co-uk/deliverator/blob/master/DLCS.Application/Behaviour/API/ApplyImagePatchBehaviour.cs#L19
            
            // This?
            dbImage.Reference1 = patch.Reference1;
            dbImage.Reference2 = patch.Reference2;
            dbImage.Reference3 = patch.Reference3;
            dbImage.NumberReference1 = patch.NumberReference1;
            dbImage.NumberReference2 = patch.NumberReference2;
            dbImage.NumberReference3 = patch.NumberReference3;
            
            // Or this?
            // if (patch.Reference1 != null)
            // {
            //     dbImage.Reference1 = patch.Reference1;
            // } // etc

            if (patch.Origin.HasText() && patch.Origin != dbImage.Origin)
            {
                dbImage.Origin = patch.Origin;
                requiresReingest = true;
            }
            
            // This illustrates the problem; it needs to be nullable. Caller might supply "" to clear tags.
            if (patch.Tags != null) 
            {
                // We can't put a HasConversion into EF DlcsContext because Dapper needs to map this too.
                dbImage.Tags = patch.Tags;
            }
            
            if (patch.Roles != null)
            {
                // We can't put a HasConversion into EF DlcsContext because Dapper needs to map this too.
                dbImage.Roles = patch.Roles;
            }

            // Here too - if the JSON payload of the patch doesn't supply this, patch.MaxUnauthorised will be 0
            // which might inadvertently clear a set value.
            dbImage.MaxUnauthorised = patch.MaxUnauthorised;

            // This on the other hand is OK, because a missing or "" value should not clear the value the asset has.
            if (patch.MediaType.HasText())
            {
                dbImage.MediaType = patch.MediaType;
            }

            if (patch.ThumbnailPolicy.HasText() && patch.ThumbnailPolicy != dbImage.ThumbnailPolicy)
            {
                dbImage.ThumbnailPolicy = patch.ThumbnailPolicy;
                // requiresReingest = true; NO, because we'll re-create thumbs on demand - "backfill"
            }
            
            if (patch.ImageOptimisationPolicy.HasText() && patch.ImageOptimisationPolicy != dbImage.ImageOptimisationPolicy)
            {
                dbImage.ImageOptimisationPolicy = patch.ImageOptimisationPolicy;
                requiresReingest = true; // YES, because we've changed the way this image should be processed
            }
            
            // In Deliverator, this removes the imagelocation from in-memory and possibly other ImageLocationStores
            // this operation parameter is temporary while we figure out whether the behaviour actually needs to be different for put/patch.
            await assetRepository.Put(dbImage, cancellationToken, "PATCH");

            var after = await assetRepository.GetAsset(patch.Id);
            await messageBus.SendAssetModifiedNotification(before, after);
            if (requiresReingest)
            {
                await messageBus.SendIngestAssetRequest(new IngestAssetRequest(after, DateTime.Now));
            }
            return after;
        }
    }

}