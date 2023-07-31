 using FluentValidation;

namespace API.Features.CustomHeaders.Validation;


public class HydraCustomHeaderValidator : AbstractValidator<DLCS.HydraModel.CustomHeader>
{
    public HydraCustomHeaderValidator()
    {
        RuleFor(ch => ch.Id)
            .Empty()
            .WithMessage(ch => $"DLCS must allocate named query id, but id {ch.Id} was supplied");
        RuleFor(nq => nq.CustomerId)
            .Empty()
            .WithMessage("Should not include user id");
        RuleFor(ch => ch.Key)
            .NotEmpty()
            .WithMessage("A key is required");
        RuleFor(nq => nq.Value)
            .NotEmpty()
            .WithMessage("A value is required");
    }
}