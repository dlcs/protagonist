using FluentValidation;

namespace API.Features.NamedQueries.Converters;

public class HydraNamedQueryValidator : AbstractValidator<DLCS.HydraModel.NamedQuery>
{
    public HydraNamedQueryValidator()
    {
        RuleFor(nq => nq.Id)
            .Empty()
            .WithMessage(nq => $"DLCS must allocate named query id, but id {nq.Id} was supplied");
        RuleFor(nq => nq.CustomerId)
            .Empty()
            .WithMessage("Should not include user id");
        RuleFor(nq => nq.Template)
            .NotEmpty()
            .WithMessage("A template is required");
        RuleFor(nq => nq.Template)
            .Must(n => n.Contains('='))
            .WithMessage("named query requires at least 1 parameter");
        RuleSet("create", () =>
        {
            RuleFor(nq => nq.Name)
                .NotEmpty()
                .WithMessage("A name is required");
        });
        RuleSet("update", () =>
        {
            RuleFor(nq => nq.Name)
                .Empty()
                .WithMessage("You cannot change the name of a named query");
        });
    }
}