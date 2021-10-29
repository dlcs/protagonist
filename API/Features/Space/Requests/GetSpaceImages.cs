using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using API.Client;
using API.Converters;
using API.Features.Image;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Features.Space.Requests
{
    public class GetSpaceImages : IRequest<PageOfAssets>
    {
        public GetSpaceImages(int page, int pageSize, int spaceId, int? customerId = null, string? orderBy = null)
        {
            Page = page;
            PageSize = pageSize;
            CustomerId = customerId;
            SpaceId = spaceId;
            OrderBy = orderBy;
        }
        
        public int SpaceId { get; set; }
        public int? CustomerId { get; }
        public int Page { get; }
        public int PageSize { get; }
        public string OrderBy { get; }
    }

    public class GetSpaceImagesHandler : IRequestHandler<GetSpaceImages, PageOfAssets>
    {
        private readonly DlcsContext dbContext;
        private readonly ClaimsPrincipal principal;
        private readonly ILogger logger;
        
        public GetSpaceImagesHandler(
            DlcsContext dbContext, 
            ClaimsPrincipal principal,
            ILogger<GetAllSpacesHandler> logger)
        {
            this.dbContext = dbContext;
            this.principal = principal;
            this.logger = logger;
        }
        
        public async Task<PageOfAssets> Handle(GetSpaceImages request, CancellationToken cancellationToken)
        {
            int? customerId = request.CustomerId ?? principal.GetCustomerId();
            var result = new PageOfAssets
            {
                Page = request.Page,
                Total = await dbContext.Images.CountAsync(
                    a => a.Customer == customerId && a.Space == request.SpaceId, cancellationToken: cancellationToken),
                Assets = await dbContext.Images.AsNoTracking()
                    .Where(a => a.Customer == customerId && a.Space == request.SpaceId)
                    .AsOrderedAssetQuery(request.OrderBy)
                    .Skip((request.Page - 1) * request.PageSize)
                    .Take(request.PageSize)
                    .ToListAsync(cancellationToken: cancellationToken)
            };
            return result;
        }
    }
}