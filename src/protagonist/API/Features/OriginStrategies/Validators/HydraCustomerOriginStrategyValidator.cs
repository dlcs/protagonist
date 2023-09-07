using DLCS.Core.Enum;
using DLCS.Model.Customers;
using FluentValidation;

namespace API.Features.OriginStrategies.Validators;

/// <summary>
/// Validator for model sent to POST/PUT /originStrategies/{id}
/// </summary>
public class HydraCustomerOriginStrategyValidator : AbstractValidator<DLCS.HydraModel.CustomerOriginStrategy>
{
    public HydraCustomerOriginStrategyValidator()
    {
        RuleFor(s => s.Id)
            .Empty()
            .WithMessage(s => $"DLCS must allocate named origin strategy id, but id {s.Id} was supplied");
        RuleFor(s => s.CustomerId)
            .Empty()
            .WithMessage("Should not include user id");
        RuleSet("create", () =>
        {
            RuleFor(s => s.OriginStrategy)
                .NotEmpty()
                .WithMessage(s => "An origin strategy must be specified");
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
                .When(s => s.OriginStrategy != OriginStrategyType.SFTP.GetDescription() || 
                           s.OriginStrategy != OriginStrategyType.SFTP.GetDescription())
                .WithMessage("Credentials can only be specified when when using basic-http-authentication or SFTP as an origin strategy");
            RuleFor(s => s.Regex)
                .NotEmpty()
                .WithMessage("Regex cannot be empty");
        });
    }
}