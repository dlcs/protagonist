using System.Collections.Generic;
using API.Infrastructure;
using API.Settings;
using DLCS.Core.Types;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Options;

namespace API.Features.Customer.Validation;

public class AdjunctIdListValidator : AbstractValidator<Dictionary<AssetId, List<string>>?>
{
    public AdjunctIdListValidator(IOptions<ApiSettings> apiSettings)
    {
        RuleFor(c => c)
            .NotEmpty().WithMessage("Members cannot be empty");
        
        RuleFor(c => c)
            .Must(m => m?.All(kvp => kvp.Value.Distinct().Count() == kvp.Value.Count) ?? true)
            .WithMessage((_, mem) =>
            {
                var dupes = mem!.Where(kvp => kvp.Value.Distinct().Count() != kvp.Value.Count).ToList();
                // duplicate grioup by causes only duplicate id's to be outputted, instead of all values
                return $"Members contains {dupes.Count} duplicate Id(s): {string.Join(",", dupes.Select(d => $"asset Id: {d.Key} : adjunct id: {string.Join(',', d.Value.GroupBy(v => v).Where(v => v.Count() > 1).Select(v => v.Key))}"))}";
            });
        
        var maxBatch = apiSettings.Value.MaxImageListSize;
        RuleFor(c => c)
            .Must(m => m!.SelectMany(a => a.Value).Count() <= maxBatch)
            .WithMessage($"Maximum adjuncts in single batch is {maxBatch}");
    }
    
    protected override bool PreValidate(ValidationContext<Dictionary<AssetId, List<string>>?> context, ValidationResult result)
    {
        return context.PreValidate(result);
    }
}
