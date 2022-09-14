using System.Linq;
using API.Settings;
using DLCS.Core.Collections;
using DLCS.HydraModel;
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
            .Must(m => (m?.Length ?? 0) < apiSettings.Value.MaxBatchSize)
            .WithMessage($"Maximum assets in single batch is {maxBatch}");

        RuleForEach(c => c.Members).SetValidator(new QueuePostImageValidator());
    }
}

public class QueuePostImageValidator : AbstractValidator<DLCS.HydraModel.Image>
{
    public QueuePostImageValidator()
    {
        // Required fields
        RuleFor(a => a.ModelId).NotEmpty().WithMessage("Asset Id cannot be empty");
        RuleFor(a => a.Space).NotEmpty().WithMessage("Space cannot be empty");
        RuleFor(a => a.MediaType).NotEmpty().WithMessage("Media type must be specified");
        RuleFor(a => a.Family).NotEmpty().WithMessage("Family must be specified");

        // System edited fields
        RuleFor(a => a.Batch).Empty().WithMessage("Should not include batch");
        RuleFor(a => a.Width).Empty().WithMessage("Should not include width");
        RuleFor(a => a.Height).Empty().WithMessage("Should not include height");
        RuleFor(a => a.Duration).Empty().WithMessage("Should not include duration");
        RuleFor(a => a.Finished).Empty().WithMessage("Should not include finished");
        RuleFor(a => a.Created).Empty().WithMessage("Should not include created");
        
        // Other validation
        RuleFor(a => a.MediaType)
            .Must(mediaType => mediaType.StartsWith("video/") || mediaType.StartsWith("audio/"))
            .When(a => a.Family == AssetFamily.Timebased)
            .WithMessage("Timebased assets must have mediaType starting video/ or audio/");
    }
}