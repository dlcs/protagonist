using FluentValidation;

namespace API.Features.Customer.Validation;

/// <summary>
/// Validator for model sent to PATCH /customer/{id}
/// </summary>
public class CustomerPatchValidator : AbstractValidator<DLCS.HydraModel.Customer>
{
    public CustomerPatchValidator()
    {
        // Note - this seems a bit excessive but it maps what is in Deliverator
        RuleFor(c => c.Id).Empty().WithMessage("Should not include customer id");
        RuleFor(c => c.Name).Empty().WithMessage("Should not include name");
        RuleFor(c => c.Administrator).Empty().WithMessage("Should not include administrator field");
        RuleFor(c => c.Created).Empty().WithMessage("Should not include created field");
        RuleFor(c => c.Keys).Empty().WithMessage("Should not include keys");
        RuleFor(c => c.AcceptedAgreement).Empty().WithMessage("Should not include acceptedAgreement field");
        
        RuleFor(c => c.DisplayName).NotEmpty().WithMessage("DisplayName cannot be empty");
    }
}