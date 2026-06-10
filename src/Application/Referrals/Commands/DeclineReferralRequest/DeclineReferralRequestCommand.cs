using MediatR;

namespace JbNet.Application.Referrals.Commands.DeclineReferralRequest;

/// <summary>Any chain participant declines. Reason and identity are never surfaced to the job seeker.</summary>
public sealed record DeclineReferralRequestCommand(
    string RequestId,
    string ActingUserId
) : IRequest<DeclineReferralRequestResult>;

public sealed record DeclineReferralRequestResult(string RequestId, string Status);
