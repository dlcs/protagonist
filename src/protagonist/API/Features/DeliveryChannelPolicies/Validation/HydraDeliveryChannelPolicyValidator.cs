using FluentValidation;

namespace API.Features.DeliveryChannelPolicies.Validation;

/// <summary>
/// Validator for model sent to POST /deliveryChannelPolicies and PUT/PATCH /deliveryChannelPolicies/{id}
/// </summary>
public class HydraDeliveryChannelPolicyValidator : AbstractValidator<DLCS.HydraModel.DeliveryChannelPolicy>
{
    private readonly string[] allowedDeliveryChannels = {"iiif-img", "iiif-av", "thumbs"};
    
    public HydraDeliveryChannelPolicyValidator()
    {
        RuleFor(p => p.Id)
            .Empty()
            .WithMessage(p => $"DLCS must allocate named origin strategy id, but id {p.Id} was supplied");
        RuleSet("post", () =>
        {
            RuleFor(c => c.Channel)
                .Empty().WithMessage("'name' is required");         
        });
        RuleSet("put", () =>
        {
            RuleFor(c => c.Channel)
                .NotEmpty().WithMessage("'name' is not permitted");         
        });
        RuleSet("patch", () =>
        {
            RuleFor(c => c.Channel)
                .Empty().WithMessage("'Name' cannot be modified in a PATCH operation");         
            RuleFor(c => c.Channel)
                .Empty().WithMessage("'Channel' cannot be modified in a PATCH operation");         
        });
        RuleFor(p => p.Channel)
            .Must(c => c == null || allowedDeliveryChannels.Contains(c))
            .WithMessage(p => $"'{p.Channel}' is not a supported delivery channel");
        RuleFor(p => p.Modified)
            .Empty().WithMessage(c => $"'policyModified' is not permitted");
        RuleFor(p => p.Created)
            .Empty().WithMessage(c => $"'policyCreated' is not permitted");
    }
}