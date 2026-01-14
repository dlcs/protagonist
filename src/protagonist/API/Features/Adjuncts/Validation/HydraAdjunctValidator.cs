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
        
        RuleFor(a => a.ExternalId)
            .NotEmpty()
            .Must(a => Uri.IsWellFormedUriString(a, UriKind.Absolute))
            .WithMessage("'externalId' is required and must be a well formed URI");
        
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
        
        RuleForEach(a => a.Language)
            .MaximumLength(3)
            .WithMessage("All 'language' values must be 3 characters or less");
    }

    private readonly List<string> validIIIFLinkTypes =
        Enum.GetValues<IIIFLinkType>().Select(a => a.GetDescription()).ToList();
}
