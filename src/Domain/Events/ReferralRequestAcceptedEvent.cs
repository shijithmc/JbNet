using JbNet.Domain.ValueObjects;

namespace JbNet.Domain.Events;

/// <summary>Raised when the final referrer accepts and will refer the candidate internally.</summary>
public sealed record ReferralRequestAcceptedEvent(
    string EventId,
    DateTimeOffset OccurredAt,
    RequestId RequestId,
    UserId JobSeekerId,
    UserId ReferrerId,
    string ReferrerName,
    string CompanyName,
    string JobTitle
) : IDomainEvent;
