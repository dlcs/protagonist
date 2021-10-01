using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using API.Client;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Portal.Features.Spaces.Models;

namespace Portal.Features.Spaces.Requests
{
    /// <summary>
    /// Request to get details of all spaces available for current user.
    /// </summary>
    public class GetPageOfSpaces : IRequest<PageOfSpaces>
    {
        public GetPageOfSpaces(int page, int pageSize, int? customerId = null)
        {
            Page = page;
            PageSize = pageSize;
            CustomerId = customerId;
        }

        public int? CustomerId { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }

    public class GetAllSpacesHandler : IRequestHandler<GetPageOfSpaces, PageOfSpaces>
    {
        private readonly DlcsContext dbContext;
        private readonly ClaimsPrincipal principal;
        private readonly ILogger logger;

        public GetAllSpacesHandler(DlcsContext dbContext, ClaimsPrincipal principal,
            ILogger<GetAllSpacesHandler> logger)
        {
            this.dbContext = dbContext;
            this.principal = principal;
            this.logger = logger;
        }

        public async Task<PageOfSpaces> Handle(GetPageOfSpaces request, CancellationToken cancellationToken)
        {
            int? customerId = request.CustomerId ?? principal.GetCustomerId();
            var result = new PageOfSpaces();
            result.Page = request.Page;
            result.Total = dbContext.Spaces
                .AsNoTracking()
                .Count(s => s.Customer == customerId);
            result.Spaces = dbContext.Spaces.AsNoTracking()
                .Where(s => s.Customer == customerId)
                .OrderBy(s => s.Id)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize);
            return result;
        }
    }
}