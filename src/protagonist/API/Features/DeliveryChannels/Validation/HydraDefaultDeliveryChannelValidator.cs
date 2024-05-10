using DLCS.Model.Assets;
using FluentValidation;

namespace API.Features.DeliveryChannels.Validation;

public class HydraDefaultDeliveryChannelValidator : AbstractValidator<DLCS.HydraModel.DefaultDeliveryChannel>
{
    public HydraDefaultDeliveryChannelValidator()
    {
        RuleFor(d => d.Id)
            .Empty()
            .WithMessage(d => $"DLCS must allocate the default delivery channel id, but id {d.Id} was supplied");

        RuleFor(d => d.Channel)
            .NotEmpty()
            .WithMessage("A channel is required");

        RuleFor(d => d.Channel)
            .Must(c => AssetDeliveryChannels.IsValidChannel(c))
            .WithMessage(d => $"delivery channel {d.Channel} is not a valid delivery channel");
        
        RuleFor(d => d.Policy)
            .NotEmpty()
            .WithMessage("A policy is required");
        
        RuleFor(d => d.MediaType)
            .NotEmpty()
            .WithMessage("A media type is required");
        
        RuleFor(d => d.MediaType)
            .Must(x => x != null && !x.Contains('?'))
            .WithMessage("'?' is a forbidden character");
    }
}