using JbNet.Domain.Enums;
using JbNet.Domain.Events;
using JbNet.Domain.Exceptions;
using JbNet.Domain.ValueObjects;

namespace JbNet.Domain.Aggregates.Referrals;

/// <summary>
/// ReferralRequest aggregate. Owns the full referral chain state machine.
/// Invariants: only one active request per (seeker, job); max 2 hops; terminal states are final.
/// </summary>
public sealed class ReferralRequest
{
    public const int ExpiryDays = 7;
    public const int MaxHops = 2;

    private readonly List<IDomainEvent> _domainEvents = [];
    private readonly List<ReferralHop> _hops = [];

    public RequestId Id { get; private set; }
    public UserId JobSeekerId { get; private set; }
    public JobId JobId { get; private set; }
    public string CompanyName { get; private set; }
    public string JobTitle { get; private set; }
    public string ResumeS3Key { get; private set; }
    public string? PersonalNote { get; private set; }
    public ReferralStatus Status { get; private set; }
    public int CurrentHopIndex { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyList<ReferralHop> Hops => _hops.AsReadOnly();
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    private ReferralRequest() { }

    public static ReferralRequest Create(
        RequestId id,
        UserId jobSeekerId,
        JobId jobId,
        string companyName,
        string jobTitle,
        string resumeS3Key,
        string? personalNote,
        IReadOnlyList<UserId> hopParticipants,
        DateTimeOffset createdAt)
    {
        if (hopParticipants.Count == 0 || hopParticipants.Count > MaxHops)
            throw new ArgumentException($"Chain must have 1–{MaxHops} participants.", nameof(hopParticipants));
        if (string.IsNullOrWhiteSpace(resumeS3Key)) throw new ArgumentException("Resume S3 key required.", nameof(resumeS3Key));

        var request = new ReferralRequest
        {
            Id = id,
            JobSeekerId = jobSeekerId,
            JobId = jobId,
            CompanyName = companyName,
            JobTitle = jobTitle,
            ResumeS3Key = resumeS3Key,
            PersonalNote = personalNote?.Trim(),
            Status = ReferralStatus.Sent,
            CurrentHopIndex = 0,
            CreatedAt = createdAt,
            ExpiresAt = createdAt.AddDays(ExpiryDays),
            UpdatedAt = createdAt
        };

        for (int i = 0; i < hopParticipants.Count; i++)
            request._hops.Add(ReferralHop.Create(i, hopParticipants[i], createdAt));

        request._domainEvents.Add(new ReferralRequestCreatedEvent(
            Guid.NewGuid().ToString(), createdAt,
            id, jobSeekerId, jobId,
            hopParticipants[0], companyName, jobTitle));

        return request;
    }

    /// <summary>Intermediary or final referrer forwards the request to the next hop.</summary>
    public void Forward(UserId actingUserId, string? note, DateTimeOffset actionAt)
    {
        EnsureActive();
        var currentHop = GetCurrentHop();
        EnsureActingUserIsCurrentParticipant(actingUserId, currentHop);

        currentHop.MarkForwarded(note, actionAt);

        bool isFinalHop = CurrentHopIndex == _hops.Count - 1;

        if (isFinalHop)
        {
            // There is no next hop — final referrer accepting is a separate action.
            // Forward on the last hop means "reached final referrer."
            Status = ReferralStatus.ReachedFinalReferrer;
        }
        else
        {
            Status = ReferralStatus.Forwarded;
            CurrentHopIndex++;
        }

        UpdatedAt = actionAt;

        _domainEvents.Add(new ReferralRequestForwardedEvent(
            Guid.NewGuid().ToString(), actionAt,
            Id, actingUserId,
            _hops.Count > CurrentHopIndex ? _hops[CurrentHopIndex].ParticipantId : actingUserId,
            currentHop.Index, isFinalHop, CompanyName, JobTitle));
    }

    /// <summary>Final referrer accepts and will internally refer the candidate.</summary>
    public void Accept(UserId actingUserId, DateTimeOffset actionAt)
    {
        if (Status != ReferralStatus.ReachedFinalReferrer)
            throw new RequestNotActiveException(Id.Value);

        var finalHop = _hops[^1];
        EnsureActingUserIsCurrentParticipant(actingUserId, finalHop);

        finalHop.MarkAccepted(actionAt);
        Status = ReferralStatus.Accepted;
        UpdatedAt = actionAt;

        _domainEvents.Add(new ReferralRequestAcceptedEvent(
            Guid.NewGuid().ToString(), actionAt,
            Id, JobSeekerId, actingUserId,
            string.Empty, // Referrer name resolved at Application layer
            CompanyName, JobTitle));
    }

    /// <summary>Any participant declines. Reason and identity are never surfaced to the job seeker.</summary>
    public void Decline(UserId actingUserId, DateTimeOffset actionAt)
    {
        EnsureActive();
        var currentHop = GetCurrentHop();
        EnsureActingUserIsCurrentParticipant(actingUserId, currentHop);

        currentHop.MarkDeclined(actionAt);
        Status = ReferralStatus.Declined;
        UpdatedAt = actionAt;

        _domainEvents.Add(new ReferralRequestDeclinedEvent(
            Guid.NewGuid().ToString(), actionAt,
            Id, JobSeekerId, CompanyName, JobTitle));
    }

    /// <summary>Job seeker withdraws their request. Purges resume access for all intermediaries.</summary>
    public void Withdraw(UserId actingUserId, DateTimeOffset actionAt)
    {
        if (actingUserId != JobSeekerId)
            throw new UnauthorizedHopActionException(actingUserId.Value, Id.Value);
        EnsureActive();

        Status = ReferralStatus.Withdrawn;
        UpdatedAt = actionAt;

        _domainEvents.Add(new ReferralRequestWithdrawnEvent(
            Guid.NewGuid().ToString(), actionAt,
            Id, JobSeekerId, ResumeS3Key));
    }

    /// <summary>Called by expiry Lambda after 7 days of inaction at current hop.</summary>
    public void Expire(DateTimeOffset actionAt)
    {
        if (!IsActive) return;

        var currentHop = GetCurrentHop();
        currentHop.MarkExpired(actionAt);
        Status = ReferralStatus.Expired;
        UpdatedAt = actionAt;

        _domainEvents.Add(new ReferralRequestExpiredEvent(
            Guid.NewGuid().ToString(), actionAt,
            Id, JobSeekerId, currentHop.ParticipantId, CompanyName, JobTitle));
    }

    /// <summary>Returns true if the given user is an active participant at any hop in this request.</summary>
    public bool IsActiveParticipant(UserId userId)
    {
        if (!IsActive) return false;
        return _hops.Any(h => h.ParticipantId == userId);
    }

    public void ClearDomainEvents() => _domainEvents.Clear();

    public bool IsActive => Status is
        ReferralStatus.Sent or
        ReferralStatus.Forwarded or
        ReferralStatus.ReachedFinalReferrer;

    private void EnsureActive()
    {
        if (!IsActive) throw new RequestNotActiveException(Id.Value);
    }

    private ReferralHop GetCurrentHop()
    {
        if (CurrentHopIndex >= _hops.Count)
            throw new RequestNotActiveException(Id.Value);
        return _hops[CurrentHopIndex];
    }

    private static void EnsureActingUserIsCurrentParticipant(UserId actingUserId, ReferralHop hop)
    {
        if (hop.ParticipantId != actingUserId)
            throw new UnauthorizedHopActionException(actingUserId.Value, hop.ParticipantId.Value);
    }
}
