using DLCS.Core;
using DLCS.Core.Collections;
using DLCS.Model.Assets;
using DLCS.Model.Policies;
using FluentValidation;

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

        When(a => !a.DeliveryChannels.IsNullOrEmpty(), DeliveryChannelDependantValidation)
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
        RuleForEach(a => a.DeliveryChannels)
            .Must(dc => AssetDeliveryChannels.All.Contains(dc))
            .WithMessage($"DeliveryChannel must be one of {AssetDeliveryChannels.AllString}");
    }

    // Validation rules that depend on DeliveryChannel being populated
    private void DeliveryChannelDependantValidation()
    {
        RuleFor(a => a.ImageOptimisationPolicy)
            .Must(iop => !KnownImageOptimisationPolicy.IsNoOpIdentifier(iop))
            .When(a => !a.DeliveryChannels.ContainsOnly(AssetDeliveryChannels.File))
            .WithMessage(
                $"ImageOptimisationPolicy {KnownImageOptimisationPolicy.NoneId} only valid for 'file' delivery channel");

        RuleFor(a => a.Width)
            .Empty()
            .WithMessage("Should not include width")
            .Unless(a =>
                a.DeliveryChannels.ContainsOnly(AssetDeliveryChannels.File) && !MIMEHelper.IsAudio(a.MediaType));
        
        RuleFor(a => a.Height)
            .Empty()
            .WithMessage("Should not include height")
            .Unless(a => 
                a.DeliveryChannels.ContainsOnly(AssetDeliveryChannels.File) && !MIMEHelper.IsAudio(a.MediaType));
        
        RuleFor(a => a.Duration)
            .Empty()
            .WithMessage("Should not include duration")
            .Unless(a =>
                a.DeliveryChannels.ContainsOnly(AssetDeliveryChannels.File) && !MIMEHelper.IsImage(a.MediaType));

        RuleFor(a => a.ImageOptimisationPolicy)
            .Must(iop => !KnownImageOptimisationPolicy.IsUseOriginalIdentifier(iop))
            .When(a => !a.DeliveryChannels!.Contains(AssetDeliveryChannels.Image))
            .WithMessage(
                $"ImageOptimisationPolicy '{KnownImageOptimisationPolicy.UseOriginalId}' only valid for image delivery-channel");
    }
}