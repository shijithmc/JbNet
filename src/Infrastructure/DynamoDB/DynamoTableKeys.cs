namespace JbNet.Infrastructure.DynamoDB;

/// <summary>Centralises all DynamoDB PK/SK/GSI key construction for the single-table design.</summary>
internal static class DynamoTableKeys
{
    // ── Primary keys ──────────────────────────────────────────────────────────
    public static string UserPk(string userId)    => $"USER#{userId}";
    public static string UserProfileSk()           => "PROFILE";
    public static string UserRefSk(string reqId)   => $"REF#{reqId}";
    public static string UserConnSk(string targetId) => $"CONN#{targetId}";
    public static string UserCooldownSk(string jobId) => $"COOLDOWN#JOB#{jobId}";

    public static string JobPk(string jobId)       => $"JOB#{jobId}";
    public static string JobMetadataSk()            => "METADATA";

    // Partition key for paginated job feed — all jobs share this PK so they can be paginated by SK
    public static string JobFeedPk()               => "PARTITION#JOBS";
    public static string JobFeedSk(DateTimeOffset postedAt, string jobId)
        => $"DATE#{postedAt:yyyy-MM-dd'T'HH:mm:ss'Z'}#{jobId}";

    public static string ReferralPk(string reqId)  => $"REF#{reqId}";
    public static string ReferralMetadataSk()       => "METADATA";

    // ── GSI1 — pending requests by participant ────────────────────────────────
    public const string Gsi1IndexName = "GSI1-PendingByParticipant";
    public static string Gsi1Pk(string userId)     => $"PENDING#{userId}";
    public static string Gsi1Sk(DateTimeOffset createdAt) => $"CREATED#{createdAt:O}";

    public static string DeviceTokenSk(string tokenId) => $"DEVICE#{tokenId}";

    // ── Attribute names ───────────────────────────────────────────────────────
    public const string PkAttr        = "PK";
    public const string SkAttr        = "SK";
    public const string Gsi1PkAttr    = "GSI1PK";
    public const string Gsi1SkAttr    = "GSI1SK";
    public const string EntityTypeAttr = "EntityType";
    public const string TtlAttr       = "TTL";
    public const string DataAttr      = "Data";
}
