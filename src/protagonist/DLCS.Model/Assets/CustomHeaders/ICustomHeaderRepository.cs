using System.Collections.Generic;
using System.Threading.Tasks;

namespace DLCS.Model.Assets.CustomHeaders;

public interface ICustomHeaderRepository
{
    /// <summary>
    /// Load all CustomHeaders for customer
    /// </summary>
    public Task<IEnumerable<CustomHeader>> GetForCustomer(int customerId);
}