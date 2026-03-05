using System.Collections.Generic;
using API.Settings;
using DLCS.Core.Enum;
using DLCS.Model.Assets;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace API.Features.Adjuncts.Validation;

public class HydraAdjunctValidator : AbstractValidator<DLCS.HydraModel.Adjunct>
{
    public HydraAdjunctValidator(IOptions<ApiSettings> apiSettings)
    {
        RuleFor(a => a.IIIFLink).NotEmpty()
            .WithMessage("'iiifLink' is required");
        RuleFor(a => a.IIIFLink).Must(a => validIIIFLinkTypes.Contains(a!))
            .When(a => a.IIIFLink != null)
            .WithMessage($"Valid values for 'iiifLink' are '{string.Join("', '", validIIIFLinkTypes)}'");
        
        RuleFor(a => a.MediaType).NotEmpty()
            .WithMessage("'mediaType' is required");
        
        RuleFor(a=>a)
            .Must(a=>a is ({ExternalId:not null} or {Origin:not null}) and not {ExternalId:not null, Origin:not null})
            .WithMessage("Either 'origin' or 'externalId' is required, but not both");
        
        RuleFor(a => a.ExternalId)
            .Must(a => Uri.IsWellFormedUriString(a, UriKind.Absolute))
            .When(a => a.ExternalId != null)
            .WithMessage("'externalId' must be a well formed URI");
        
        RuleFor(a => a.Origin)
            .Must(a => Uri.IsWellFormedUriString(a, UriKind.Absolute))
            .When(a => a.Origin != null)
            .WithMessage("'origin' must be a well formed URI");
        
        RuleFor(a => a.ModelId)
            .NotEmpty()
            .WithMessage("Adjunct identifier could not be found");
        
        RuleFor(a => a.ModelId)
            .MaximumLength(Adjunct.MaxIdLength)
            .WithMessage($"Adjunct id must be {Adjunct.MaxIdLength} characters or less");
        
        RuleFor(a => a.ModelId)
            .Must(a => !apiSettings.Value.DoesResourceIdContainRestrictedCharacters(a))
            .WithMessage($"Adjunct id contains at least one of the following restricted characters. Invalid values are: {new string(apiSettings.Value.RestrictedResourceIdCharacters)}");
        
        RuleFor(a => a.Type)
            .NotEmpty()
            .WithMessage("'@type' is required");

        const int maximumLength = 10;
        RuleForEach(a => a.Language)
            .MaximumLength(maximumLength)
            .WithMessage($"All 'language' values must be {maximumLength} characters or less. e.g. ISO language codes, sub-codes or 'none'");
        
        RuleFor(a => a.Type)
            .Must(t =>  t == "AnnotationPage")
            .When(a => a.IIIFLink == IIIFLinkType.Annotations.GetDescription())
            .WithMessage($"When 'iiifLink' is '{IIIFLinkType.Annotations.GetDescription()}', the '@type' must be 'AnnotationPage'");
    }

    private readonly List<string> validIIIFLinkTypes =
        Enum.GetValues<IIIFLinkType>().Select(a => a.GetDescription()).ToList();
}
