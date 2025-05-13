using API.Features.Customer;
using API.Features.Customer.Validation;
using API.Infrastructure.Requests;
using API.Settings;
using DLCS.Model;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Options;

namespace API.Features.Image.Validation;

/// <summary>
/// Validator for <see cref="BulkPatch{IdentiferOnly}"/>
/// </summary>
public class BulkAssetPatchValidator : AbstractValidator<BulkPatch<IdentifierOnly>>
{
    public BulkAssetPatchValidator(IOptions<ApiSettings> apiSettings)
    {
        RuleFor(bp => bp.Members).SetValidator(new ImageIdListValidator(apiSettings));

        RuleFor(bp => bp.Field)
            .Equal(BulkAssetPatcher.SupportedFields.ManifestField)
            .WithMessage(f => $"Unsupported field '{f.Field}'");
    }
    
    protected override bool PreValidate(ValidationContext<BulkPatch<IdentifierOnly>> context, ValidationResult result) 
    {
        if (context.InstanceToValidate.Members == null) 
        {
            result.Errors.Add(new ValidationFailure("Members", "Member cannot be null"));
            return false;
        }
        return true;
    }
}
