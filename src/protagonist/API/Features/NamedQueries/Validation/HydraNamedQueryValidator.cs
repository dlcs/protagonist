using FluentValidation;

namespace API.Features.NamedQueries.Converters;

public class HydraNamedQueryValidator : AbstractValidator<DLCS.HydraModel.NamedQuery>
{
    public HydraNamedQueryValidator()
    {
        RuleFor(nq => nq.ModelId)
            .Empty()
            .WithMessage(nq => $"DLCS must allocate named query id, but id {nq.ModelId} was supplied");
        RuleFor(nq => nq.CustomerId)
            .Empty()
            .WithMessage("Should not include user id");
        RuleFor(nq => nq.Name)
            .NotEmpty()
            .WithMessage("Name cannot be empty");
        RuleFor(nq => nq.Template)
            .NotEmpty()
            .WithMessage("Template cannot be empty");
    }
}