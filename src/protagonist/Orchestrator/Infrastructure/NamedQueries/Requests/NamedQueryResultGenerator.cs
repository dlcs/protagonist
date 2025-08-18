using System.Threading.Tasks;
using DLCS.Model.Assets.NamedQueries;
using DLCS.Model.PathElements;
using DLCS.Repository.NamedQueries;

namespace Orchestrator.Infrastructure.NamedQueries.Requests;

/// <summary>
/// Class responsible for parsing CustomerPathElement and getting NamedQueryResult.
/// </summary>
public class NamedQueryResultGenerator
{ 
    private readonly IPathCustomerRepository pathCustomerRepository;
    private readonly NamedQueryConductor namedQueryConductor;
    
    public NamedQueryResultGenerator(
        IPathCustomerRepository pathCustomerRepository,
        NamedQueryConductor namedQueryConductor)
    {
        this.pathCustomerRepository = pathCustomerRepository;
        this.namedQueryConductor = namedQueryConductor;
    }

    public async Task<PathElementNamedQueryResultContainer<T>> GetNamedQueryResult<T>(IBaseNamedQueryRequest request)
        where T : ParsedNamedQuery
    {
        var customerPathElement = await pathCustomerRepository.GetCustomerPathElement(request.CustomerPathValue);

        var namedQueryResult =
            await namedQueryConductor.GetNamedQueryResult<T>(request.NamedQuery,
                customerPathElement.Id, request.NamedQueryArgs);
        return new PathElementNamedQueryResultContainer<T>(namedQueryResult, customerPathElement);
    }
}

/// <summary>
/// Class representing a <see cref="NamedQueryResult{T}"/> alongside <see cref="CustomerPathElement"/>
/// </summary>
/// <typeparam name="T">Type of named query result</typeparam>
public class PathElementNamedQueryResultContainer<T>
    where T : ParsedNamedQuery
{
    public NamedQueryResult<T> NamedQueryResult { get; }
    public CustomerPathElement CustomerPathElement { get; }
    
    public PathElementNamedQueryResultContainer(NamedQueryResult<T> namedQueryResult, CustomerPathElement customerPathElement)
    {
        NamedQueryResult = namedQueryResult;
        CustomerPathElement = customerPathElement;
    }
}