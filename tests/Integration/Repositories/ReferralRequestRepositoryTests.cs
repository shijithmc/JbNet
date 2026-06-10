using FluentAssertions;
using JbNet.Domain.Aggregates.Referrals;
using JbNet.Domain.Enums;
using JbNet.Domain.Repositories;
using JbNet.Domain.ValueObjects;
using JbNet.Tests.Integration.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace JbNet.Tests.Integration.Repositories;

/// <summary>
/// Integration tests for <see cref="IReferralRequestRepository"/> backed by DynamoDB Local.
/// </summary>
public sealed class ReferralRequestRepositoryTests : IClassFixture<DynamoDbFixture>
{
    private readonly IReferralRequestRepository _repo;
    private static readonly DateTimeOffset Now = new(2025, 6, 1, 0, 0, 0, TimeSpan.Zero);

    public ReferralRequestRepositoryTests(DynamoDbFixture fixture)
    {
        _repo = fixture.Services.GetRequiredService<IReferralRequestRepository>();
    }

    private static ReferralRequest BuildRequest(string reqId, string seekerId, string participantId) =>
        ReferralRequest.Create(
            RequestId.From(reqId),
            UserId.From(seekerId),
            JobId.From("job-001"),
            "TestCo",
            "SWE",
            $"resumes/{seekerId}/cv.pdf",
            null,
            new[] { UserId.From(participantId) },
            Now);

    [Fact]
    public async Task SaveAsync_NewRequest_GetByIdAsync_ReturnsRequest()
    {
        // Arrange
        var req = BuildRequest("req-int-001", "seeker-001", "referrer-001");

        // Act
        await _repo.SaveAsync(req, CancellationToken.None);
        var retrieved = await _repo.GetByIdAsync(RequestId.From("req-int-001"), CancellationToken.None);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Id.Value.Should().Be("req-int-001");
        retrieved.Status.Should().Be(ReferralStatus.Sent);
        retrieved.Hops.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentRequest_ReturnsNull()
    {
        var result = await _repo.GetByIdAsync(RequestId.From("req-does-not-exist"), CancellationToken.None);
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetActiveByJobSeekerAsync_ActiveRequest_ReturnsList()
    {
        // Arrange
        var seekerId = "seeker-int-002";
        var req = BuildRequest("req-int-002", seekerId, "referrer-002");
        await _repo.SaveAsync(req, CancellationToken.None);

        // Act
        var active = await _repo.GetActiveByJobSeekerAsync(UserId.From(seekerId), CancellationToken.None);

        // Assert
        active.Should().Contain(r => r.Id.Value == "req-int-002");
    }

    [Fact]
    public async Task GetPendingByParticipantAsync_SentRequest_ReturnsList()
    {
        // Arrange
        var participantId = "referrer-int-003";
        var req = BuildRequest("req-int-003", "seeker-int-003", participantId);
        await _repo.SaveAsync(req, CancellationToken.None);

        // Act
        var pending = await _repo.GetPendingByParticipantAsync(UserId.From(participantId), CancellationToken.None);

        // Assert
        pending.Should().Contain(r => r.Id.Value == "req-int-003");
    }

    [Fact]
    public async Task SaveAsync_AfterDecline_StatusPersistedAsDeclined()
    {
        // Arrange
        var req = BuildRequest("req-int-004", "seeker-int-004", "referrer-int-004");
        await _repo.SaveAsync(req, CancellationToken.None);

        // Act — decline the request
        req.Decline(UserId.From("referrer-int-004"), Now.AddHours(1));
        req.ClearDomainEvents();
        await _repo.SaveAsync(req, CancellationToken.None);
        var retrieved = await _repo.GetByIdAsync(RequestId.From("req-int-004"), CancellationToken.None);

        // Assert
        retrieved!.Status.Should().Be(ReferralStatus.Declined);
    }

    [Fact]
    public async Task GetExpiredCandidatesAsync_OldActiveRequest_ReturnedAsCandidate()
    {
        // Arrange — create a request in the past (> 7 days old)
        var oldCreatedAt = Now.AddDays(-8);
        var req = ReferralRequest.Create(
            RequestId.From("req-int-exp-001"),
            UserId.From("seeker-exp-001"),
            JobId.From("job-exp-001"),
            "OldCo",
            "Engineer",
            "resumes/seeker-exp-001/cv.pdf",
            null,
            new[] { UserId.From("referrer-exp-001") },
            oldCreatedAt);
        req.ClearDomainEvents();
        await _repo.SaveAsync(req, CancellationToken.None);

        // Act
        var candidates = await _repo.GetExpiredCandidatesAsync(7, CancellationToken.None);

        // Assert
        candidates.Should().Contain(r => r.Id.Value == "req-int-exp-001");
    }

    [Fact]
    public async Task SaveCooldownAsync_GetCooldownAsync_ReturnsCooldown()
    {
        // Arrange
        var userId = UserId.From("seeker-cd-001");
        var jobId  = JobId.From("job-cd-001");
        var cooldown = ReferralCooldown.Create(userId, jobId, Now);

        // Act
        await _repo.SaveCooldownAsync(cooldown, CancellationToken.None);
        var retrieved = await _repo.GetCooldownAsync(userId, jobId, CancellationToken.None);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.UserId.Value.Should().Be("seeker-cd-001");
        retrieved.JobId.Value.Should().Be("job-cd-001");
    }
}
