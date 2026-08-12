using DLCS.Core.Enum;
using DLCS.Model.Customers;
using FluentValidation;

namespace API.Features.OriginStrategies.Validators;

/// <summary>
/// Validator for model sent to POST/PUT /originStrategies/{id}
/// </summary>
public class HydraCustomerOriginStrategyValidator : AbstractValidator<DLCS.HydraModel.CustomerOriginStrategy>
{
    private static readonly string S3Ambient = OriginStrategyType.S3Ambient.GetDescription();
    private static readonly string BasicHttp = OriginStrategyType.BasicHttp.GetDescription();
    private static readonly string Sftp = OriginStrategyType.SFTP.GetDescription();

    public HydraCustomerOriginStrategyValidator()
    {
        RuleFor(s => s.Id)
            .Empty()
            .WithMessage(s => $"DLCS must allocate named origin strategy id, but id {s.Id} was supplied");
        RuleFor(s => s.CustomerId)
            .Empty()
            .WithMessage("Should not include customer id");
        RuleSet("create", () =>
        {
            RuleFor(s => s.OriginStrategy)
                .NotEmpty()
                .WithMessage(_ => "An origin strategy must be specified");
            RuleFor(s => s.Optimised)
                .NotEqual(true)
                .When(s => s.OriginStrategy != S3Ambient)
                .WithMessage("'Optimised' is only applicable when using s3-ambient as an origin strategy");
            RuleFor(s => s.Credentials)
                .NotEmpty()
                .When(s => s.OriginStrategy == BasicHttp || s.OriginStrategy == Sftp)
                .WithMessage(s => $"Credentials must be specified when using {s.OriginStrategy} as an origin strategy");
            RuleFor(s => s.Credentials)
                .Empty()
                .When(s => s.OriginStrategy != BasicHttp && s.OriginStrategy != Sftp)
                .WithMessage(
                    $"Credentials can only be specified when using {BasicHttp} or {Sftp} as an origin strategy");
            RuleFor(s => s.Regex)
                .NotEmpty()
                .WithMessage("Regex cannot be empty");
        });
    }
}
