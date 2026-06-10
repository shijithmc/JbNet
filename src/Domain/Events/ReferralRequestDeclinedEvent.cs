using JbNet.Domain.ValueObjects;

namespace JbNet.Domain.Events;

/// <summary>Raised when any participant declines. Job seeker notified with path-unavailable message — no reason or decliner identity disclosed.</summary>
public sealed record ReferralRequestDeclinedEvent(
    string EventId,
    DateTimeOffset OccurredAt,
    RequestId RequestId,
    UserId JobSeekerId,
    string CompanyName,
    string JobTitle
) : IDomainEvent;
