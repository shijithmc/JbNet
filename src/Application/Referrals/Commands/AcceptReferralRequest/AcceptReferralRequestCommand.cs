using MediatR;

namespace JbNet.Application.Referrals.Commands.AcceptReferralRequest;

/// <summary>Final referrer accepts the referral request — they will internally refer the candidate.</summary>
public sealed record AcceptReferralRequestCommand(
    string RequestId,
    string ActingUserId
) : IRequest<AcceptReferralRequestResult>;

public sealed record AcceptReferralRequestResult(string RequestId, string ReferrerName, string CompanyName);
