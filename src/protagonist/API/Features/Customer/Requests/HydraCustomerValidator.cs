using System;
using System.Collections.Generic;
using System.Linq;
using DLCS.Core.Strings;

namespace API.Features.Customer.Requests;

/// <summary>
/// Performs basic checks on a new POSTed or PUT Hydra Customer.
/// Does not perform data integrity/constraint checks.
/// </summary>
public static class HydraCustomerValidator
{
    /// <summary>
    /// Validates a customer is ready to be passed to the Customer repository
    /// </summary>
    /// <param name="newCustomer">The new Hydra Customer</param>
    /// <returns>A list of string error messages</returns>
    public static string[] GetNewHydraCustomerErrors(DLCS.HydraModel.Customer newCustomer)
    {
        var errorList = new List<string>();
        if (newCustomer.ModelId > 0)
        {
            errorList.Add($"DLCS must allocate customer id, but id {newCustomer.ModelId} was supplied.");
        }

        if (string.IsNullOrEmpty(newCustomer.Name))
        {
            errorList.Add($"A new customer must have a name (url part).");
        }
            
        if (string.IsNullOrEmpty(newCustomer.DisplayName))
        {
            errorList.Add($"A new customer must have a Display name (label).");
        }
            
        if (newCustomer.Administrator == true)
        {
            errorList.Add($"You can't attempt to create an Administrator customer.");
        }

        if (newCustomer.Keys.HasText())
        {
            errorList.Add( $"You can't supply API Keys at customer creation time.");
        }
        
        if (Enum.GetNames(typeof(DLCS.HydraModel.Customer.ReservedNames)).Any(n =>
                String.Equals(n, newCustomer.Name, StringComparison.CurrentCultureIgnoreCase)))
        {
            errorList.Add($"Name field [{newCustomer.Name}] cannot be a reserved word.");
        }

        return errorList.ToArray();
    }
}