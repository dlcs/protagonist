using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Model.Assets;
using DLCS.Web.Auth;
using MediatR;
using Microsoft.Extensions.Logging;

namespace API.Features.Space.Requests
{
    public class GetSpaceImages : IRequest<GetSpaceImagesResult>
    {
        public GetSpaceImages(bool ascending, int page, int pageSize, int spaceId, int? customerId = null, string? orderBy = null)
        {
            Page = page;
            PageSize = pageSize;
            CustomerId = customerId;
            SpaceId = spaceId;
            OrderBy = orderBy;
            Ascending = ascending;
        }
        
        public int SpaceId { get; set; }
        public int? CustomerId { get; }
        public int Page { get; }
        public int PageSize { get; }
        public string OrderBy { get; }
        public bool Ascending { get; }
    }

    public class GetSpaceImagesResult
    {
        public PageOfAssets? PageOfAssets { get; set; }
        public List<string>? Errors { get; set; }
        public bool SpaceExistsForCustomer { get; set; }
    }

    public class GetSpaceImagesHandler : IRequestHandler<GetSpaceImages, GetSpaceImagesResult>
    {
        private readonly IAssetRepository assetRepository;
        private readonly ClaimsPrincipal principal;
        private readonly ILogger logger;
        
        public GetSpaceImagesHandler(
            IAssetRepository assetRepository, 
            ClaimsPrincipal principal,
            ILogger<GetAllSpacesHandler> logger)
        {
            this.assetRepository = assetRepository;
            this.principal = principal;
            this.logger = logger;
        }
        
        public async Task<GetSpaceImagesResult> Handle(GetSpaceImages request, CancellationToken cancellationToken)
        {
            int? customerId = request.CustomerId ?? principal.GetCustomerId();
            if (customerId == null)
            {
                throw new BadRequestException("No customer Id supplied");
            }
            
            var pageOfAssets = await assetRepository.GetPageOfAssets(
                customerId.Value, request.SpaceId,
                request.Page, request.PageSize,
                request.OrderBy, request.Ascending,
                cancellationToken);

            if (pageOfAssets == null)
            {
                return new GetSpaceImagesResult
                {
                    Errors = new List<string>() { "Space not found" },
                    SpaceExistsForCustomer = false
                };
            }

            return new GetSpaceImagesResult
            {
                PageOfAssets = pageOfAssets,
                SpaceExistsForCustomer = true
            };
        }
    }
}