using MediatR;

namespace JbNet.Application.Referrals.Commands.CreateReferralRequest;

/// <summary>Creates a new referral request through a selected chain path.</summary>
public sealed record CreateReferralRequestCommand(
    string JobSeekerId,
    string JobId,
    /// <summary>Ordered list of user IDs forming the chain: [intermediary, finalReferrer] or [finalReferrer] for 1-hop.</summary>
    IReadOnlyList<string> HopParticipantIds,
    string? PersonalNote
) : IRequest<CreateReferralRequestResult>;

public sealed record CreateReferralRequestResult(string RequestId, string Status);
