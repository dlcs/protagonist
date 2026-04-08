using API.Features.Adjuncts.Infrastructure;
using API.Settings;
using DLCS.Model;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Options;

namespace API.Features.Customer.Validation;

public class AdjunctIdListValidator : AbstractValidator<AdjunctIdentifierOnly[]?>
{
    public AdjunctIdListValidator(IOptions<ApiSettings> apiSettings)
    {
        RuleFor(c => c)
            .NotEmpty().WithMessage("Members cannot be empty");
        
        RuleFor(c => c)
            .Must(m => m?.ConvertToDictionary().Any(kvp => kvp.Value.Distinct().Count() == kvp.Value.Count) ?? true)
            .WithMessage((_, mem) =>
            {
                var dupes = mem!.ConvertToDictionary().Where(kvp => kvp.Value.Distinct().Count() != kvp.Value.Count).ToList();
                return $"Members contains {dupes.Count} duplicate Id(s): {string.Join(",", dupes.Select(d => $"asset Id: {d.Key} : adjunct id: {string.Join(',', d.Value.Distinct())}"))}";
            });
        
        var maxBatch = apiSettings.Value.MaxImageListSize;
        RuleFor(c => c)
            .Must(m => m.Flatten().Count() <= maxBatch)
            .WithMessage($"Maximum adjuncts in single batch is {maxBatch}");
    }
    
    protected override bool PreValidate(ValidationContext<AdjunctIdentifierOnly[]?> context, ValidationResult result) 
    {
        if (context.InstanceToValidate == null) 
        {
            result.Errors.Add(new ValidationFailure("", "Members cannot be null"));
            return false;
        }
        return true;
    }
}
