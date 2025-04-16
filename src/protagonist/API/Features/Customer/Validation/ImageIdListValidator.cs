using API.Settings;
using DLCS.Core.Collections;
using DLCS.Model;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace API.Features.Customer.Validation;

/// <summary>
/// Validator for body sent to POST /customer/{id}/allImages
/// </summary>
public class ImageIdListValidator : AbstractValidator<IdentifierOnly[]?>
{
    public ImageIdListValidator(IOptions<ApiSettings> apiSettings)
    {
        RuleFor(c => c)
            .NotEmpty().WithMessage("Members cannot be empty");
        
        RuleFor(c => c)
            .Must(m => m.IsNullOrEmpty() || m!.Select(a => a.Id).Distinct().Count() == m!.Length)
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
}
