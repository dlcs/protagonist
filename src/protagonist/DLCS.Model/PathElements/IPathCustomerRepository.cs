using System.Threading.Tasks;

namespace DLCS.Model.PathElements
{
    public interface IPathCustomerRepository
    {
        Task<CustomerPathElement> GetCustomer(string customerPart);
    }
}
