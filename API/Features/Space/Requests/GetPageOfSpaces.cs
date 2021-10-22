using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using API.Client;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Features.Space.Requests
{
    /// <summary>
    /// Request to get details of all spaces available for current user.
    /// </summary>
    public class GetPageOfSpaces : IRequest<PageOfSpaces>
    {
        public GetPageOfSpaces(int page, int pageSize, int? customerId = null, string? orderBy = null)
        {
            Page = page;
            PageSize = pageSize;
            CustomerId = customerId;
            OrderBy = orderBy;
        }

        public int? CustomerId { get; }
        public int Page { get; }
        public int PageSize { get; }
        public string OrderBy { get; }
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
            var result = new PageOfSpaces
            {
                Page = request.Page,
                Total = dbContext.Spaces
                    .AsNoTracking()
                    .Count(s => s.Customer == customerId),
                Spaces = dbContext.Spaces.AsNoTracking()
                    .Where(s => s.Customer == customerId)
                    .OrderBy(s => s.Id) 
                    .Skip((request.Page - 1) * request.PageSize)
                    .Take(request.PageSize)
                    .ToList()
            };
            // In Deliverator the following is a sub-select. But I suspect that this is not significantly slower.
            var scopes = result.Spaces.Select(s => s.Id.ToString());
            var counters = await dbContext.EntityCounters.AsNoTracking()
                .Where(ec => ec.Customer == customerId && ec.Type == "space-images")
                .Where(ec => scopes.Contains(ec.Scope))
                .ToDictionaryAsync(ec => ec.Scope, ec => ec.Next, cancellationToken: cancellationToken);
            foreach (var space in result.Spaces)
            {
                space.ApproximateNumberOfImages = counters[space.Id.ToString()];
            }
            return result;
        }
    }
}