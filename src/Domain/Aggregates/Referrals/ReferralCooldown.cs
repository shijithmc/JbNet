using JbNet.Domain.ValueObjects;

namespace JbNet.Domain.Aggregates.Referrals;

/// <summary>
/// Records a cooldown for a (user, job) pair after a terminal referral state.
/// TTL = 30 days from creation. Enforced before allowing a new referral request.
/// </summary>
public sealed class ReferralCooldown
{
    public const int CooldownDays = 30;

    public UserId UserId { get; private set; }
    public JobId JobId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }

    private ReferralCooldown() { }

    public static ReferralCooldown Create(UserId userId, JobId jobId, DateTimeOffset createdAt) =>
        new()
        {
            UserId = userId,
            JobId = jobId,
            CreatedAt = createdAt,
            ExpiresAt = createdAt.AddDays(CooldownDays)
        };

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;
}
