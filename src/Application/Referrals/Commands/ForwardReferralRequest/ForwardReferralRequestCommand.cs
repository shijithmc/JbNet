using MediatR;

namespace JbNet.Application.Referrals.Commands.ForwardReferralRequest;

/// <summary>Intermediary forwards (or final referrer passes along) the referral request.</summary>
public sealed record ForwardReferralRequestCommand(
    string RequestId,
    string ActingUserId,
    string? ForwardNote
) : IRequest<ForwardReferralRequestResult>;

public sealed record ForwardReferralRequestResult(string RequestId, string Status);
