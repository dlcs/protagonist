using API.Settings;
using DLCS.Core;
using DLCS.Core.Collections;
using DLCS.Model.Assets;
using DLCS.Model.Policies;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace API.Features.Image.Validation;

/// <summary>
/// Validator for model Hydra images
/// </summary>
public class HydraImageValidator : AbstractValidator<DLCS.HydraModel.Image>
{
    public HydraImageValidator(IOptions<ApiSettings> apiSettings)
    {
        RuleSet("patch", () =>
        {
            RuleFor(p => p.DeliveryChannels)
                .Must(a => a!.Any())
                .When(a => a.DeliveryChannels != null)
                .WithMessage("'deliveryChannels' cannot be an empty array when updating an existing asset via PATCH");
        });
        
        RuleSet("create", () =>
        {
            RuleFor(a => a.MediaType).NotEmpty().WithMessage("Media type must be specified");
        });
        
        When(a => !a.WcDeliveryChannels.IsNullOrEmpty(), DeliveryChannelDependantValidation)
            .Otherwise(() =>
            {
                RuleFor(a => a.Width).Empty().WithMessage("Should not include width");
                RuleFor(a => a.Height).Empty().WithMessage("Should not include height");
                RuleFor(a => a.Duration).Empty().WithMessage("Should not include duration");
            });
        
        When(a => !a.DeliveryChannels.IsNullOrEmpty(), ImageDeliveryChannelDependantValidation);
        
        // System edited fields
        RuleFor(a => a.Batch).Empty().WithMessage("Should not include batch");
        RuleFor(a => a.Finished).Empty().WithMessage("Should not include finished");
        RuleFor(a => a.Created).Empty().WithMessage("Should not include created");

        // Other validation
        RuleFor(a => a.WcDeliveryChannels).Must(d => d.IsNullOrEmpty())
            .When(_ => !apiSettings.Value.DeliveryChannelsEnabled)
            .WithMessage("Delivery channels are disabled");

        RuleForEach(a => a.WcDeliveryChannels)
            .Must(AssetDeliveryChannels.IsValidChannel)
            .WithMessage($"DeliveryChannel must be one of {AssetDeliveryChannels.AllString}");
    }

    private void ImageDeliveryChannelDependantValidation()
    {
        RuleForEach(a => a.DeliveryChannels)
            .Must(dc => AssetDeliveryChannels.IsValidChannel(dc.Channel))
            .WithMessage($"DeliveryChannel must be one of {AssetDeliveryChannels.AllString}");

        RuleFor(a => a.DeliveryChannels)
            .Must(d => d.All(d => d.Channel != AssetDeliveryChannels.None))
            .When(a => a.DeliveryChannels!.Length > 1)
            .WithMessage("If 'none' is the specified channel, then no other delivery channels are allowed");

        RuleForEach(a => a.DeliveryChannels)
            .Must(c => !string.IsNullOrEmpty(c.Channel))
            .WithMessage("'channel' must be specified when supplying delivery channels to an asset");
            
        RuleForEach(a => a.DeliveryChannels)
            .Must((a, c) => AssetDeliveryChannels.IsChannelValidForMediaType(c.Channel, a.MediaType!))
            .When(a => !string.IsNullOrEmpty(a.MediaType))
            .WithMessage((a,c) => $"'{c.Channel}' is not a valid delivery channel for asset of type \"{a.MediaType}\"");
    
        RuleForEach(a => a.DeliveryChannels)
            .Must((a, c) => a.DeliveryChannels!.Count(dc => dc.Channel == c.Channel) <= 1)
            .WithMessage("'deliveryChannels' cannot contain duplicate channels.");
    }

    // Validation rules that depend on DeliveryChannel being populated
    private void DeliveryChannelDependantValidation()
    {
        RuleFor(a => a.ImageOptimisationPolicy)
            .Must(iop => !KnownImageOptimisationPolicy.IsNoOpIdentifier(iop))
            .When(a => !a.WcDeliveryChannels.ContainsOnly(AssetDeliveryChannels.File))
            .WithMessage(
                $"ImageOptimisationPolicy {KnownImageOptimisationPolicy.NoneId} only valid for 'file' delivery channel");

        RuleFor(a => a.Width)
            .Empty()
            .WithMessage("Should not include width")
            .Unless(a =>
                a.WcDeliveryChannels.ContainsOnly(AssetDeliveryChannels.File) && !MIMEHelper.IsAudio(a.MediaType));
        
        RuleFor(a => a.Height)
            .Empty()
            .WithMessage("Should not include height")
            .Unless(a => 
                a.WcDeliveryChannels.ContainsOnly(AssetDeliveryChannels.File) && !MIMEHelper.IsAudio(a.MediaType));
        
        RuleFor(a => a.Duration)
            .Empty()
            .WithMessage("Should not include duration")
            .Unless(a =>
                a.WcDeliveryChannels.ContainsOnly(AssetDeliveryChannels.File) && !MIMEHelper.IsImage(a.MediaType));

        RuleFor(a => a.ImageOptimisationPolicy)
            .Must(iop => !KnownImageOptimisationPolicy.IsUseOriginalIdentifier(iop))
            .When(a => !a.WcDeliveryChannels!.Contains(AssetDeliveryChannels.Image))
            .WithMessage(
                $"ImageOptimisationPolicy '{KnownImageOptimisationPolicy.UseOriginalId}' only valid for image delivery-channel");
    }
}