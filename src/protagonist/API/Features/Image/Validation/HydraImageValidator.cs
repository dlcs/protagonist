using DLCS.Core;
using DLCS.Model.Assets;
using DLCS.Model.Policies;
using FluentValidation;
using AssetFamily = DLCS.HydraModel.AssetFamily;

namespace API.Features.Image.Validation;

/// <summary>
/// Validator for model Hydra images
/// </summary>
public class HydraImageValidator : AbstractValidator<DLCS.HydraModel.Image>
{
    public HydraImageValidator()
    {
        // Required fields
        RuleFor(a => a.MediaType).NotEmpty().WithMessage("Media type must be specified");
        RuleFor(a => a.Family).NotEmpty().WithMessage("Family must be specified");

        // ImageOptimisationPolicy dependant validation
        When(a => ImageOptimisationPolicyX.IsNotProcessedIdentifier(a.ImageOptimisationPolicy) && a.Family != AssetFamily.File,
                () =>
                {
                    When(a => !MIMEHelper.IsAudio(a.MediaType), () =>
                    {
                        RuleFor(a => a.Width)
                            .NotEmpty()
                            .WithMessage("Width cannot be empty if 'none' imageOptimisationPolicy specified");
                        RuleFor(a => a.Height)
                            .NotEmpty()
                            .WithMessage("Height cannot be empty if 'none' imageOptimisationPolicy specified");
                    });

                    RuleFor(a => a.Duration)
                        .NotEmpty()
                        .When(a => a.Family == AssetFamily.Timebased)
                        .WithMessage(
                            "Duration cannot be empty if 'none' imageOptimisationPolicy specified for timebased asset");
                    RuleFor(a => a.Duration)
                        .Empty()
                        .When(a => a.Family == AssetFamily.Image)
                        .WithMessage("Should not include duration");
                })
            .Otherwise(() =>
            {
                RuleFor(a => a.Width).Empty().WithMessage("Should not include width");
                RuleFor(a => a.Height).Empty().WithMessage("Should not include height");
                RuleFor(a => a.Duration).Empty().WithMessage("Should not include duration");
            });

        // System edited fields
        RuleFor(a => a.Batch).Empty().WithMessage("Should not include batch");
        RuleFor(a => a.Finished).Empty().WithMessage("Should not include finished");
        RuleFor(a => a.Created).Empty().WithMessage("Should not include created");
        
        // Other validation
        RuleFor(a => a.MediaType)
            .Must(mediaType => MIMEHelper.IsVideo(mediaType) || MIMEHelper.IsAudio(mediaType))
            .When(a => a.Family == AssetFamily.Timebased)
            .WithMessage("Timebased assets must have mediaType starting video/ or audio/");
        RuleForEach(a => a.DeliveryChannel)
            .Must(dc => AssetDeliveryChannels.All.Contains(dc))
            .WithMessage($"DeliveryChannel must be one of {AssetDeliveryChannels.AllString}");
    }
}