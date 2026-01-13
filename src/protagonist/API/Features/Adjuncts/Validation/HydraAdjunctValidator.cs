using System.Collections.Generic;
using DLCS.Core.Enum;
using DLCS.Model.Assets;
using FluentValidation;

namespace API.Features.Adjuncts.Validation;

public class HydraAdjunctValidator : AbstractValidator<DLCS.HydraModel.Adjunct>
{
    public HydraAdjunctValidator()
    {
        RuleFor(a => a.IIIFLink).NotEmpty()
            .WithMessage("'iiifLink' is required");
        RuleFor(a => a.IIIFLink).Must(a => validIIIFLinkTypes.Contains(a!))
            .When(a => a.IIIFLink != null)
            .WithMessage($"Valid values for 'iiifLink' are '{string.Join("', '", validIIIFLinkTypes)}'");
        
        RuleFor(a => a.MediaType).NotEmpty()
            .WithMessage("'mediaType' is required");
        
        RuleFor(a => a.ExternalId)
            .NotEmpty()
            .Must(a => Uri.IsWellFormedUriString(a, UriKind.Absolute))
            .WithMessage("'externalId' is required and must be a well formed URI");
        
        RuleFor(a => a.ModelId)
            .NotEmpty()
            .WithMessage("Adjunct identifier could not be found");
        
        RuleFor(a => a.Type)
            .NotEmpty()
            .WithMessage("'@type' is required");
    }

    private readonly List<string> validIIIFLinkTypes =
        Enum.GetValues<IIIFLinkType>().Select(a => a.GetDescription()).ToList();
}
