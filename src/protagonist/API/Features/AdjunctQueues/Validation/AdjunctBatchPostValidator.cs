using API.Features.Adjuncts.Validation;
using API.Settings;
using DLCS.Core.Collections;
using FluentValidation;
using Hydra.Collections;
using Microsoft.Extensions.Options;

namespace API.Features.AdjunctQueues.Validation;

/// <summary>
/// Validator for model sent to POST /customers/{id}/adjunctQueue
/// </summary>
public class AdjunctBatchPostValidator : AbstractValidator<HydraCollection<DLCS.HydraModel.Adjunct>>
{
    public AdjunctBatchPostValidator(IOptions<ApiSettings> apiSettings,
        HydraAdjunctValidator adjunctValidator)
    {
        RuleFor(c => c.Members)
            .NotEmpty().WithMessage("Members cannot be empty");

        var maxBatch = apiSettings.Value.MaxBatchSize;
        RuleFor(c => c.Members)
            .Must(m => (m?.Length ?? 0) <= maxBatch)
            .WithMessage($"Maximum adjuncts in single batch is {maxBatch}");

        RuleFor(c => c.Members)
            .Must(m => m.IsNullOrEmpty() || m.All(a => !string.IsNullOrEmpty(a.Asset)))
            .WithMessage("All members must have an 'asset' field");

        RuleFor(c => c.Members)
            .Must(m => m.IsNullOrEmpty() || m.Select(a => $"{a.Asset}/{a.ModelId}").Distinct().Count() == m.Length)
            .WithMessage((_, mem) =>
            {
                var dupes = mem!.Select(a => new { a.Asset, a.ModelId })
                    .GetDuplicates()
                    .Select(dup => $"Id:{dup.ModelId},Asset:{dup.Asset}")
                    .ToList();
                return $"Members contains {dupes.Count} duplicate adjunct(s): {string.Join(",", dupes)}";
            });

        RuleForEach(c => c.Members).SetValidator(adjunctValidator);
    }
}
