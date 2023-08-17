using System.Text.RegularExpressions;
using DLCS.Core.Enum;
using DLCS.Model.Customers;
using DLCS.Model.Policies;
using FluentValidation;

namespace API.Features.OriginStrategies.Validators;

public class HydraCustomerOriginStrategyValidator : AbstractValidator<DLCS.HydraModel.CustomerOriginStrategy>
{
    private readonly OriginStrategyType[] allowedStrategyTypes = 
        { OriginStrategyType.BasicHttp, OriginStrategyType.S3Ambient };
    
    public HydraCustomerOriginStrategyValidator()
    {
        RuleFor(s => s.Id)
            .Empty()
            .WithMessage(s => $"DLCS must allocate named query id, but id {s.Id} was supplied");
        RuleFor(s => s.CustomerId)
            .Empty()
            .WithMessage("Should not include user id");
        RuleFor(s => s.OriginStrategy)
            .NotEmpty()
            .WithMessage(s => "An origin strategy must be specified");
        RuleFor(s => s.OriginStrategy)
            .Must( s => s != null && s.IsValidEnumValue<OriginStrategyType>())
            .WithMessage(s => $"'{s.OriginStrategy}' is not a valid origin strategy");
        RuleFor(s => s.OriginStrategy)
            .Must( s => s != null && allowedStrategyTypes.Contains(s.GetEnumFromString<OriginStrategyType>()))
            .WithMessage(s => $"'{s.OriginStrategy}' is currently not supported as an origin strategy");
        RuleFor(s => s.Optimised)
            .NotEqual(true)
            .When(s => s.OriginStrategy != OriginStrategyType.S3Ambient.GetDescription())
            .WithMessage("'Optimised' is only applicable when using s3-ambient as an origin strategy");
        RuleFor(s => s.Credentials)
            .NotEmpty()
            .When(s => s.OriginStrategy == OriginStrategyType.BasicHttp.GetDescription())
            .WithMessage("Credentials must be specified when using basic-http-authentication as an origin strategy");
        RuleFor(s => s.Credentials)
            .Empty()
            .When(s => s.OriginStrategy != OriginStrategyType.BasicHttp.GetDescription())
            .WithMessage("Credentials can only be specified when when using basic-http-authentication as an origin strategy");
        RuleFor(s => s.Regex)
            .NotEmpty()
            .WithMessage("Regex cannot be empty");
    }
}