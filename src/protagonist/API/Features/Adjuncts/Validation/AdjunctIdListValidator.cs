using System.Collections.Generic;
using API.Settings;
using DLCS.Core.Collections;
using DLCS.Model;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Options;

namespace API.Features.Adjuncts.Validation;

public class AdjunctIdListValidator : AbstractValidator<AdjunctIdentifierOnly[]?>
{
    public AdjunctIdListValidator(IOptions<ApiSettings> apiSettings)
    {
        RuleFor(c => c)
            .NotEmpty().WithMessage("Members cannot be empty");
        
        RuleFor(c => c)
            .Must(m => m.IsNullOrEmpty() || m.SelectMany(a =>  new List<KeyValuePair<string, string>>()).Count() == m.Length)
            .WithMessage((_, mem) =>
            {
                var dupes = mem!.Select(a => a.Id).GetDuplicates().ToList();
                return $"Members contains {dupes.Count} duplicate Id(s): {string.Join(",", dupes)}";
            });
        
        var maxBatch = apiSettings.Value.MaxImageListSize;
        RuleFor(c => c)
            .Must(m => (m?.Length ?? 0) <= maxBatch)
            .WithMessage($"Maximum assets in single batch is {maxBatch}");
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
