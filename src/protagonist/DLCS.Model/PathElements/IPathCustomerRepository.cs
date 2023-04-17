using System.Threading.Tasks;

namespace DLCS.Model.PathElements;

public interface IPathCustomerRepository
{
    Task<CustomerPathElement> GetCustomerPathElement(string customerPart);
}
