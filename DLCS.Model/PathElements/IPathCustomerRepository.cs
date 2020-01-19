using System;
using System.Collections.Generic;
using System.Text;

namespace DLCS.Model.PathElements
{
    public interface IPathCustomerRepository
    {
        CustomerPathElement GetCustomer(string customerPart);
    }
}
