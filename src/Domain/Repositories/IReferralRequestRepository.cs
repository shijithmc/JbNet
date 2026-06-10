using JbNet.Domain.Aggregates.Referrals;
using JbNet.Domain.ValueObjects;

namespace JbNet.Domain.Repositories;

/// <summary>Persistence interface for ReferralRequest aggregate and related cooldown records.</summary>
public interface IReferralRequestRepository
{
    Task<ReferralRequest?> GetByIdAsync(RequestId requestId, CancellationToken ct);
    Task SaveAsync(ReferralRequest request, CancellationToken ct);

    /// <summary>Returns all active requests for the job seeker (status = Sent, Forwarded, ReachedFinalReferrer).</summary>
    Task<IReadOnlyList<ReferralRequest>> GetActiveByJobSeekerAsync(UserId jobSeekerId, CancellationToken ct);

    /// <summary>Returns requests pending action by the given user (they are the current hop participant).</summary>
    Task<IReadOnlyList<ReferralRequest>> GetPendingByParticipantAsync(UserId participantId, CancellationToken ct);

    /// <summary>Returns the cooldown record for a (user, job) pair, or null if none exists.</summary>
    Task<ReferralCooldown?> GetCooldownAsync(UserId userId, JobId jobId, CancellationToken ct);
    Task SaveCooldownAsync(ReferralCooldown cooldown, CancellationToken ct);

    /// <summary>Returns requests that are still active but were last updated more than <paramref name="olderThanDays"/> days ago.</summary>
    Task<IReadOnlyList<ReferralRequest>> GetExpiredCandidatesAsync(int olderThanDays, CancellationToken ct);
}
