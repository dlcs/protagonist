using System.Text.RegularExpressions;
using DLCS.Core.Strings;
using FluentValidation;

namespace API.Features.Customer.Validation; 

/// <summary>
/// Performs basic checks on a new POSTed or PUT Hydra Customer.
/// Does not perform data integrity/constraint checks.
/// </summary>
public class HydraCustomerValidator : AbstractValidator<DLCS.HydraModel.Customer>
{
    private const int NameMaxLength = 60;

    public HydraCustomerValidator()
    {
        RuleFor(c => c.ModelId)
            .Empty()
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
            .Must(name => !IsReservedWord(name!))
            .WithMessage(c=> $"Name field [{c.Name}] cannot be a reserved word.");
        
        RuleFor(c => c.Name)
            .Must(name => !StartsWithVersion(name!))
            .When(c => !string.IsNullOrEmpty(c.Name))
            .WithMessage(c=> $"Name field [{c.Name}] cannot start with a version slug.");

        RuleFor(c => c.Name).MaximumLength(NameMaxLength);
        
        RuleFor(c => c.Name)
            .Must(name => ContainsOnlyValidCharacters(name!))
            .When(c => !string.IsNullOrEmpty(c.Name))
            .WithMessage(c=> $"Name field [{c.Name}] contains invalid characters. Accepted: [a-z] [A-Z] [0-9] - _ and .");
    }
    
    private static bool IsReservedWord(string customerName)
    {
        return Enum.GetNames(typeof(DLCS.HydraModel.Customer.ReservedNames)).Any(n =>
            string.Equals(n, customerName, StringComparison.CurrentCultureIgnoreCase));
    }

    private static bool StartsWithVersion(string customerName)
    {
        const string versionRegex = @"^v\d+";
        return Regex.IsMatch(customerName, versionRegex);
    }
    
    private static bool ContainsOnlyValidCharacters(string customerName)
    {
        // NOTE - if regex changes, alter the error message
        const string validCharsRegex = @"^[a-zA-Z0-9_\-\.]+$";
        return Regex.IsMatch(customerName, validCharsRegex);
    }
}