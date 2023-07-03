using DLCS.Core.Strings;
using FluentValidation;

namespace API.Features.Customer.Validation; 

/// <summary>
/// Performs basic checks on a new POSTed or PUT Hydra Customer.
/// Does not perform data integrity/constraint checks.
/// </summary>
public class HydraCustomerValidator : AbstractValidator<DLCS.HydraModel.Customer>
{
    public HydraCustomerValidator()
    {
        RuleFor(c => c.ModelId).Empty()
            .WithMessage(c => $"DLCS must allocate customer id, but id {c.ModelId} was supplied.");
        
        RuleFor(c => c.Name)
            .NotEmpty()
            .WithMessage("A new customer must have a name (url part).");
        
        RuleFor(c => c.DisplayName)
            .NotEmpty()
            .WithMessage("A new customer must have a Display name (label).");
        
        RuleFor(c => c.Administrator)
            .NotEqual(true)
            .WithMessage("You can't attempt to create an Administrator customer.");
        
        RuleFor(c => c.Keys.HasText())
            .Equal(false)
            .WithMessage("You can't supply API Keys at customer creation time.");
        
        RuleFor(c => c.Name)
            .Must(name => !IsReservedWord(name))
            .WithMessage(c=> $"Name field [{c.Name}] cannot be a reserved word.");
    }
    
    private bool IsReservedWord(string? customerName)
    {
        return Enum.GetNames(typeof(DLCS.HydraModel.Customer.ReservedNames)).Any(n =>
            String.Equals(n, customerName, StringComparison.CurrentCultureIgnoreCase));
    }
}