using API.Settings;
using DLCS.Core.Collections;
using FluentValidation;
using Hydra.Collections;
using Microsoft.Extensions.Options;

namespace API.Features.Image.Validation;

/// <summary>
/// Validator for model sent to PATCH /customers/{customerId}/spaces/{spaceId}/images
/// </summary>
public class ImageBatchPatchValidator : AbstractValidator<HydraCollection<DLCS.HydraModel.Image>>
{
    public ImageBatchPatchValidator(IOptions<ApiSettings> apiSettings)
    {
        RuleFor(c => c.Members)
            .NotEmpty().WithMessage("Members cannot be empty");
        
        RuleFor(c => c.Members)
            .Must(m => m.IsNullOrEmpty() || m!.Select(a => a.ModelId).Distinct().Count() == m!.Length)
            .WithMessage((_, mem) =>
            {
                var dupes = mem!.Select(a => a.ModelId).GetDuplicates().ToList();
                return $"Members contains {dupes.Count} duplicate Id(s): {string.Join(",", dupes)}";
            });

        var maxBatch = apiSettings.Value.MaxBatchSize;
        RuleFor(c => c.Members)
            .Must(m => (m?.Length ?? 0) <= maxBatch)
            .WithMessage($"Maximum assets in single batch is {maxBatch}");
        
        RuleForEach(c => c.Members).ChildRules(members =>
        {
            members.RuleFor(a => a.ModelId).NotEmpty().WithMessage("All assets require a ModelId");
            members.RuleFor(a => a.Origin).Empty().WithMessage("Origin cannot be set in a bulk patching operation");
            members.RuleFor(a => a.ImageOptimisationPolicy).Empty().WithMessage("Image optimisation policies cannot be set in a bulk patching operation");
            members.RuleFor(a => a.MaxUnauthorised).Empty().WithMessage("MaxUnauthorised cannot be set in a bulk patching operation");
            members.RuleFor(a => a.DeliveryChannels).Empty().WithMessage("Delivery channels cannot be set in a bulk patching operation");
        });
    }
}