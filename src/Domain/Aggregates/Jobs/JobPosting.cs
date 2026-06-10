using JbNet.Domain.ValueObjects;

namespace JbNet.Domain.Aggregates.Jobs;

/// <summary>
/// JobPosting aggregate. Curated job listings. Auto-expires after 90 days.
/// Company name is the key match field for referral path discovery.
/// </summary>
public sealed class JobPosting
{
    public const int ExpiryDays = 90;

    public JobId Id { get; private set; }
    public string CompanyName { get; private set; }
    public string Title { get; private set; }
    public string Description { get; private set; }
    public string Location { get; private set; }
    public string? ExternalUrl { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset PostedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }

    private JobPosting() { }

    public static JobPosting Create(
        JobId id,
        string companyName,
        string title,
        string description,
        string location,
        string? externalUrl,
        DateTimeOffset postedAt)
    {
        if (string.IsNullOrWhiteSpace(companyName)) throw new ArgumentException("Company name required.", nameof(companyName));
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title required.", nameof(title));

        return new JobPosting
        {
            Id = id,
            CompanyName = companyName.Trim(),
            Title = title.Trim(),
            Description = description?.Trim() ?? string.Empty,
            Location = location?.Trim() ?? string.Empty,
            ExternalUrl = externalUrl?.Trim(),
            IsActive = true,
            PostedAt = postedAt,
            ExpiresAt = postedAt.AddDays(ExpiryDays)
        };
    }

    public void Deactivate() => IsActive = false;

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;

    /// <summary>Company name normalised to lowercase for case-insensitive matching in path discovery.</summary>
    public string NormalisedCompanyName => CompanyName.Trim().ToLowerInvariant();
}
