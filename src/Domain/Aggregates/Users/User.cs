using JbNet.Domain.Enums;
using JbNet.Domain.Exceptions;
using JbNet.Domain.ValueObjects;

namespace JbNet.Domain.Aggregates.Users;

/// <summary>
/// User aggregate. Owns profile data, resume metadata, and active request count.
/// Connections are a separate read-model keyed off UserId.
/// </summary>
public sealed class User
{
    public const int MaxActiveRequests = 5;
    public const int MaxConnections = 2000;

    public UserId Id { get; private set; }
    public string FullName { get; private set; }
    public string Email { get; private set; }
    public string Headline { get; private set; }
    public string? EmployerName { get; private set; }
    public string? City { get; private set; }
    public string? ProfilePhotoUrl { get; private set; }
    public string? ResumeS3Key { get; private set; }
    public string? ResumeFileName { get; private set; }
    public long? ResumeSizeBytes { get; private set; }
    public int ActiveReferralCount { get; private set; }
    public int ConnectionCount { get; private set; }
    public UserRole Role { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private User() { }

    public static User Create(
        UserId id,
        string fullName,
        string email,
        DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(fullName)) throw new ArgumentException("Full name is required.", nameof(fullName));
        if (string.IsNullOrWhiteSpace(email)) throw new ArgumentException("Email is required.", nameof(email));

        return new User
        {
            Id = id,
            FullName = fullName.Trim(),
            Email = email.Trim().ToLowerInvariant(),
            Headline = string.Empty,
            ActiveReferralCount = 0,
            ConnectionCount = 0,
            Role = UserRole.JobSeeker,
            IsActive = true,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };
    }

    public void UpdateProfile(
        string fullName,
        string headline,
        string? employerName,
        string? city,
        DateTimeOffset updatedAt)
    {
        if (string.IsNullOrWhiteSpace(fullName)) throw new ArgumentException("Full name is required.", nameof(fullName));

        FullName = fullName.Trim();
        Headline = headline?.Trim() ?? string.Empty;
        EmployerName = employerName?.Trim();
        City = city?.Trim();
        UpdatedAt = updatedAt;
    }

    public void SetResume(string s3Key, string fileName, long sizeBytes, DateTimeOffset updatedAt)
    {
        ResumeS3Key = s3Key;
        ResumeFileName = fileName;
        ResumeSizeBytes = sizeBytes;
        UpdatedAt = updatedAt;
    }

    public void RemoveResume(DateTimeOffset updatedAt)
    {
        ResumeS3Key = null;
        ResumeFileName = null;
        ResumeSizeBytes = null;
        UpdatedAt = updatedAt;
    }

    public void SetProfilePhoto(string photoUrl, DateTimeOffset updatedAt)
    {
        ProfilePhotoUrl = photoUrl;
        UpdatedAt = updatedAt;
    }

    /// <summary>Increments active referral count. Enforces MaxActiveRequests invariant.</summary>
    public void IncrementActiveReferralCount()
    {
        if (ActiveReferralCount >= MaxActiveRequests)
            throw new ActiveRequestLimitExceededException(MaxActiveRequests);

        ActiveReferralCount++;
    }

    public void DecrementActiveReferralCount()
    {
        if (ActiveReferralCount > 0) ActiveReferralCount--;
    }

    public void IncrementConnectionCount()
    {
        if (ConnectionCount >= MaxConnections)
            throw new ConnectionLimitExceededException(MaxConnections);

        ConnectionCount++;
    }

    public void DecrementConnectionCount()
    {
        if (ConnectionCount > 0) ConnectionCount--;
    }

    public void Deactivate(DateTimeOffset updatedAt)
    {
        IsActive = false;
        UpdatedAt = updatedAt;
    }
}
