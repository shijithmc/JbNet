using JbNet.Domain.ValueObjects;

namespace JbNet.Domain.Events;

/// <summary>Raised when an intermediary forwards a referral to the next hop. Triggers notification to the next participant.</summary>
public sealed record ReferralRequestForwardedEvent(
    string EventId,
    DateTimeOffset OccurredAt,
    RequestId RequestId,
    UserId ForwardedByUserId,
    UserId NextHopUserId,
    int HopIndex,
    bool IsFinalHop,
    string CompanyName,
    string JobTitle
) : IDomainEvent;
