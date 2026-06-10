using JbNet.Domain.Enums;
using JbNet.Domain.ValueObjects;

namespace JbNet.Domain.Aggregates.Referrals;

/// <summary>
/// A single hop in the referral chain. index=0 is the 1st-degree intermediary, index=1 is the final referrer.
/// </summary>
public sealed class ReferralHop
{
    public int Index { get; private set; }
    public UserId ParticipantId { get; private set; }
    public HopStatus Status { get; private set; }
    public string? ForwardNote { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ActionTakenAt { get; private set; }

    private ReferralHop() { }

    public static ReferralHop Create(int index, UserId participantId, DateTimeOffset createdAt) =>
        new()
        {
            Index = index,
            ParticipantId = participantId,
            Status = HopStatus.Pending,
            CreatedAt = createdAt
        };

    public void MarkForwarded(string? note, DateTimeOffset actionAt)
    {
        Status = HopStatus.Forwarded;
        ForwardNote = note?.Trim();
        ActionTakenAt = actionAt;
    }

    public void MarkDeclined(DateTimeOffset actionAt)
    {
        Status = HopStatus.Declined;
        ActionTakenAt = actionAt;
    }

    public void MarkExpired(DateTimeOffset actionAt)
    {
        Status = HopStatus.Expired;
        ActionTakenAt = actionAt;
    }

    public void MarkAccepted(DateTimeOffset actionAt)
    {
        Status = HopStatus.Accepted;
        ActionTakenAt = actionAt;
    }

    public bool IsPending => Status == HopStatus.Pending;
}
