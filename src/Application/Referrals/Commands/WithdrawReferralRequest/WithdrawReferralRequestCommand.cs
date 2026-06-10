using MediatR;

namespace JbNet.Application.Referrals.Commands.WithdrawReferralRequest;

/// <summary>Job seeker withdraws their active referral request. Triggers resume purge from all intermediary inboxes.</summary>
public sealed record WithdrawReferralRequestCommand(
    string RequestId,
    string JobSeekerId
) : IRequest<WithdrawReferralRequestResult>;

public sealed record WithdrawReferralRequestResult(string RequestId);
