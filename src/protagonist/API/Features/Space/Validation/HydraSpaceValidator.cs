using FluentValidation;
using FluentValidation.Results;

namespace API.Features.Space.Validation;

/// <summary>
/// Validator for models sent to POST/PUT/PATCH /customers/{customerId}/spaces.
/// </summary>
public class HydraSpaceValidator : AbstractValidator<DLCS.HydraModel.Space>
{
    /// <summary>
    /// Ruleset for validating a space that is being created, where the DLCS assigns the id
    /// </summary>
    public const string CreateRuleSet = "create";

    private const string UpdateRuleSet = "update";
    private const string RouteSpaceIdKey = "RouteSpaceId";

    public HydraSpaceValidator()
    {
        RuleSet(CreateRuleSet, () =>
        {
            RuleFor(s => s.Name)
                .NotEmpty()
                .WithMessage("A space must have a name.");

            RuleFor(s => s.ModelId)
                .Empty()
                .WithMessage("An id cannot be supplied when creating a space; the platform assigns it.");
        });

        RuleSet(UpdateRuleSet, () =>
        {
            RuleFor(s => s.ModelId)
                .Must((_, modelId, context) => modelId == GetRouteSpaceId(context))
                .When(s => s.ModelId.HasValue)
                .WithMessage("The id in the request body does not agree with the request URL.");
        });
    }

    /// <summary>
    /// Validate a space from a PUT or PATCH body, verifying that any id asserted in the body agrees with the space
    /// id from the request URL.
    /// </summary>
    public Task<ValidationResult> ValidateUpdateAsync(DLCS.HydraModel.Space space, int routeSpaceId,
        CancellationToken cancellationToken = default)
    {
        var context = ValidationContext<DLCS.HydraModel.Space>.CreateWithOptions(space,
            options => options.IncludeRuleSets("default", UpdateRuleSet));
        context.RootContextData[RouteSpaceIdKey] = routeSpaceId;

        return ValidateAsync(context, cancellationToken);
    }

    private static int? GetRouteSpaceId(ValidationContext<DLCS.HydraModel.Space> context)
        => context.RootContextData.TryGetValue(RouteSpaceIdKey, out var routeSpaceId) ? (int)routeSpaceId : null;
}
