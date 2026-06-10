using FluentValidation;

namespace JbNet.Application.Referrals.Commands.CreateReferralRequest;

public sealed class CreateReferralRequestValidator : AbstractValidator<CreateReferralRequestCommand>
{
    public CreateReferralRequestValidator()
    {
        RuleFor(x => x.JobSeekerId).NotEmpty().WithMessage("Job seeker ID required.");
        RuleFor(x => x.JobId).NotEmpty().WithMessage("Job ID required.");
        RuleFor(x => x.HopParticipantIds)
            .NotEmpty().WithMessage("At least one chain participant required.")
            .Must(ids => ids.Count <= 2).WithMessage("Chain cannot exceed 2 participants (v1).");
        RuleFor(x => x.PersonalNote)
            .MaximumLength(300).WithMessage("Personal note cannot exceed 300 characters.")
            .When(x => x.PersonalNote != null);
    }
}
