using DLCS.Model.Assets;
using FluentValidation;

namespace API.Features.Adjuncts.Validation;

public class HydraAdjunctValidator : AbstractValidator<DLCS.HydraModel.Adjunct>
{
    public HydraAdjunctValidator()
    {
        RuleFor(a => a.IIIFLink).NotEmpty()
            .WithMessage("'iiifLink' is required");
        RuleFor(a => a.MediaType).NotEmpty()
            .WithMessage("'mediaType' is required");
        
        RuleFor(a => a.ExternalId).Must(a => Uri.IsWellFormedUriString(a, UriKind.Absolute))
            .When(a => a.ExternalId != null)
            .WithMessage("'externalId' must be a well formed URI");
        
        RuleFor(a => a.IIIFLink).Must(a => Enum.IsDefined(typeof(IIIFLinkType), a))
            .When(a => a.IIIFLink != null)
            .WithMessage("Valid values for 'iiifLink' are 'SeeAlso', 'Annotations' and 'Rendering'");
        
        RuleFor(a => a).Must(a => a.Id!.Split('/').Last() == a.ModelId)
            .When(a => a.Id != null && a.ModelId != null)
            .WithMessage("'id' and '@id' must have a matching adjunct identifier");
        
        RuleFor(nq => nq.ModelId)
            .NotEmpty()
            .WithMessage("Adjunct identifier could not be found");
    }
}
