using System.Collections.Generic;
using API.Infrastructure;
using API.Settings;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Options;

namespace API.Features.Customer.Validation;

public class AdjunctIdListValidator : AbstractValidator<Dictionary<string, List<string>>?>
{
    public AdjunctIdListValidator(IOptions<ApiSettings> apiSettings)
    {
        RuleFor(c => c)
            .NotEmpty().WithMessage("Members cannot be empty");
        
        RuleFor(c => c)
            .Must(m => m?.Any(kvp => kvp.Value.Distinct().Count() == kvp.Value.Count) ?? true)
            .WithMessage((_, mem) =>
            {
                var dupes = mem!.Where(kvp => kvp.Value.Distinct().Count() != kvp.Value.Count).ToList();
                return $"Members contains {dupes.Count} duplicate Id(s): {string.Join(",", dupes.Select(d => $"asset Id: {d.Key} : adjunct id: {string.Join(',', d.Value.Distinct())}"))}";
            });
        
        var maxBatch = apiSettings.Value.MaxImageListSize;
        RuleFor(c => c)
            .Must(m => m!.SelectMany(a => a.Value).Count() <= maxBatch)
            .WithMessage($"Maximum adjuncts in single batch is {maxBatch}");
    }
    
    protected override bool PreValidate(ValidationContext<Dictionary<string, List<string>>?> context, ValidationResult result)
    {
        return context.PreValidate(result);
    }
}
