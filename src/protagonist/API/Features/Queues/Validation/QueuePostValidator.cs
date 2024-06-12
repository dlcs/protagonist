using API.Features.Image.Validation;
using API.Settings;
using DLCS.Core.Collections;
using FluentValidation;
using Hydra.Collections;
using Microsoft.Extensions.Options;

namespace API.Features.Queues.Validation;

/// <summary>
/// Validator for model sent to POST /customer/{id}/queue
/// </summary>
public class QueuePostValidator : AbstractValidator<HydraCollection<DLCS.HydraModel.Image>>
{
    public QueuePostValidator(IOptions<ApiSettings> apiSettings)
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

        RuleForEach(c => c.Members).SetValidator(new HydraImageValidator(),
            "default", "create");

        // In addition to above validation, batched updates must have ModelId + Space as this can't be taken from
        // path
        RuleForEach(c => c.Members).ChildRules(members =>
        {
            members.RuleFor(a => a.ModelId).NotEmpty().WithMessage("Asset Id cannot be empty");
            members.RuleFor(a => a.Space).NotEmpty().WithMessage("Space cannot be empty");
        });
    }
}