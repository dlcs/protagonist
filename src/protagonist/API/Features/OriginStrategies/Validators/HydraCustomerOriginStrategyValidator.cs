using DLCS.Core.Enum;
using DLCS.Core.Strings;
using DLCS.Model.Customers;
using DLCS.Repository.Customers;
using DLCS.Repository.OriginStrategies;
using FluentValidation;
using Microsoft.Extensions.Configuration;

namespace API.Features.OriginStrategies.Validators;

/// <summary>
/// Validator for model sent to POST/PUT /originStrategies/{id}
/// </summary>
public class HydraCustomerOriginStrategyValidator : AbstractValidator<DLCS.HydraModel.CustomerOriginStrategy>
{
    private const int MaxRegexLength = 1000;

    private static readonly string S3Ambient = OriginStrategyType.S3Ambient.GetDescription();
    private static readonly string BasicHttp = OriginStrategyType.BasicHttp.GetDescription();
    private static readonly string Sftp = OriginStrategyType.SFTP.GetDescription();

    public HydraCustomerOriginStrategyValidator(IConfiguration configuration)
    {
        var regexSettings = OriginStrategyRegexSettings.FromConfiguration(configuration);

        RuleFor(s => s.Id)
            .Empty()
            .WithMessage(s => $"DLCS must allocate named origin strategy id, but id {s.Id} was supplied");
        RuleFor(s => s.CustomerId)
            .Empty()
            .WithMessage("Should not include customer id");

        // Regex rules are in the default ruleset as the value can also be changed on update
        RuleFor(s => s.Regex)
            .MaximumLength(MaxRegexLength)
            .WithMessage($"Regex must be {MaxRegexLength} characters or less");
        RuleFor(s => s.Regex)
            .Custom((regex, context) => ValidateRegex(regex!, regexSettings, context))
            .When(s => s.Regex.HasText());

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

    // Origins are matched against this regex during ingest and (sometimes) orchestration, so reject anything that can't
    // be evaluated safely here rather than letting it fail later
    private static void ValidateRegex(string regex, OriginStrategyRegexSettings regexSettings,
        ValidationContext<DLCS.HydraModel.CustomerOriginStrategy> context)
    {
        if (!OriginStrategyRegex.IsValidPattern(regex, out var error))
        {
            context.AddFailure($"Regex is not a valid regular expression: {error}");
            return;
        }

        if (regexSettings.RejectBacktrackingPatterns && !OriginStrategyRegex.SupportsNonBacktracking(regex))
        {
            context.AddFailure(
                "Regex uses lookarounds, backreferences, atomic groups or conditionals. " +
                "These are not supported - rewrite the expression without them");
        }
    }
}
