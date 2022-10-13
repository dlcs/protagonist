using System.Security.Claims;
using API.Exceptions;
using DLCS.Model.Spaces;
using DLCS.Web.Auth;
using MediatR;

namespace API.Features.Space.Requests;

/// <summary>
/// Request to get details of all spaces available for current user.
/// </summary>
public class GetPageOfSpaces : IRequest<PageOfSpaces>
{
    public GetPageOfSpaces(int page, int pageSize, int? customerId = null,
        string? orderBy = null, bool descending=false)
    {
        Page = page;
        PageSize = pageSize;
        CustomerId = customerId;
        OrderBy = orderBy;
        Descending = descending;
    }

    public int? CustomerId { get; }
    public int Page { get; }
    public int PageSize { get; }
    public string? OrderBy { get; }
    public bool Descending { get; }
}

public class GetAllSpacesHandler : IRequestHandler<GetPageOfSpaces, PageOfSpaces>
{
    private readonly ISpaceRepository spaceRepository;
    private readonly ClaimsPrincipal principal;

    public GetAllSpacesHandler(ISpaceRepository spaceRepository, ClaimsPrincipal principal)
    {
        this.spaceRepository = spaceRepository;
        this.principal = principal;
    }
    
    public async Task<PageOfSpaces> Handle(GetPageOfSpaces request, CancellationToken cancellationToken)
    {
        // TODO - principal isn't checked in any other requests - should it be removed?
        int? customerId = request.CustomerId ?? principal.GetCustomerId();
        if (customerId == null)
        {
            throw new BadRequestException("No customer Id supplied");
        }
        var result = await spaceRepository.GetPageOfSpaces(customerId.Value, 
            request.Page, request.PageSize, request.OrderBy, request.Descending, cancellationToken);
        return result;
    }
    
    


}