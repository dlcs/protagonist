using System;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Strings;
using DLCS.Model.Assets;
using DLCS.Model.Messaging;
using DLCS.Repository;
using MediatR;

namespace API.Features.Image.Requests
{
    public class PatchImage : IRequest<Asset>
    {
        public DLCS.HydraModel.Image HydraImage { get; set; }
        public int CustomerId { get; set; }
        public int SpaceId { get; set; }
        public string ModelId { get; set; }
        
        public PatchImage(int customerId, int spaceId, string modelId, DLCS.HydraModel.Image hydraImage)
        {
            CustomerId = customerId;
            SpaceId = spaceId;
            ModelId = modelId;
            HydraImage = hydraImage;
        }
    }

    public class PatchImageHandler : IRequestHandler<PatchImage, DLCS.Model.Assets.Asset>
    {
        private readonly DlcsContext dbContext;
        private readonly IMessageBus messageBus;

        public PatchImageHandler(
            DlcsContext dlcsContext,
            IMessageBus messageBus)
        {
            this.dbContext = dlcsContext;
            this.messageBus = messageBus;
        }

        public async Task<Asset> Handle(PatchImage request, CancellationToken cancellationToken)
        {
            var key = $"{request.CustomerId}/{request.SpaceId}/{request.ModelId}";
            var dbImage = await dbContext.Images.FindAsync(key, cancellationToken);
            if (dbImage == null)
            {
                var apiEx = new APIException("Asset to be patched is unknown to DLCS")
                {
                    Label = "No asset for key " + key, StatusCode = 400
                };
                throw apiEx;
            }

            var before = (Asset) dbImage.ShallowCopy();
            var hydraImage = request.HydraImage;
            bool requiresReingest = false;

            // Should the DLCS metadata DB cols BE NULLABLE?
            // The below allows you to omit a value and skip patching.
            if (hydraImage.String1 != null)
            {
                dbImage.Reference1 = hydraImage.String1;
            }
            if (hydraImage.String2 != null)
            {
                dbImage.Reference2 = hydraImage.String2;
            }
            if (hydraImage.String3 != null)
            {
                dbImage.Reference3 = hydraImage.String3;
            }
            if (hydraImage.Number1 != null)
            {
                dbImage.NumberReference1 = hydraImage.Number1.Value;
            }
            if (hydraImage.Number2 != null)
            {
                dbImage.NumberReference2 = hydraImage.Number2.Value;
            }
            if (hydraImage.Number3 != null)
            {
                dbImage.NumberReference3 = hydraImage.Number3.Value;
            }

            if (hydraImage.Origin != null && hydraImage.Origin != dbImage.Origin)
            {
                dbImage.Origin = hydraImage.Origin;
                requiresReingest = true;
            }

            if (hydraImage.Tags != null)
            {
                // We can't put a HasConversion into EF DlcsContext because Dapper needs to map this too.
                dbImage.Tags = string.Join(",", hydraImage.Tags);
            }
            
            if (hydraImage.Roles != null)
            {
                // We can't put a HasConversion into EF DlcsContext because Dapper needs to map this too.
                dbImage.Roles = string.Join(",", hydraImage.Roles);
            }

            if (hydraImage.MaxUnauthorised != null)
            {
                dbImage.MaxUnauthorised = hydraImage.MaxUnauthorised.Value;
            }

            if (hydraImage.MediaType != null)
            {
                dbImage.MediaType = hydraImage.MediaType;
            }

            if (hydraImage.ThumbnailPolicy.HasText() && hydraImage.ThumbnailPolicy != dbImage.ThumbnailPolicy)
            {
                dbImage.ThumbnailPolicy = hydraImage.ThumbnailPolicy;
                // requiresReingest = true; NO, because we'll re-create thumbs on demand - "backfill"
            }
            
            if (hydraImage.ImageOptimisationPolicy.HasText() && hydraImage.ImageOptimisationPolicy != dbImage.ImageOptimisationPolicy)
            {
                dbImage.ImageOptimisationPolicy = hydraImage.ImageOptimisationPolicy;
                requiresReingest = true; // YES, because we've changed the way this image should be processed
            }
            
            await dbContext.SaveChangesAsync(cancellationToken);
            
            var after = await ImageRequestHelpers.GetImageInternal(dbContext, key, cancellationToken);
            messageBus.SendAssetModifiedNotification(before, after);
            if (requiresReingest)
            {
                messageBus.SendIngestAssetRequest(new IngestAssetRequest(after, DateTime.Now));
            }
            return after;
        }
    }

}