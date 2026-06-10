using FluentAssertions;
using JbNet.Domain.Aggregates.Users;
using JbNet.Domain.Enums;
using JbNet.Domain.Repositories;
using JbNet.Domain.ValueObjects;
using JbNet.Tests.Integration.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace JbNet.Tests.Integration.Repositories;

/// <summary>
/// Integration tests for <see cref="IConnectionRepository"/> backed by DynamoDB Local.
/// </summary>
public sealed class ConnectionRepositoryTests : IClassFixture<DynamoDbFixture>
{
    private readonly IConnectionRepository _repo;
    private readonly IUserRepository _userRepo;
    private static readonly DateTimeOffset Now = new(2025, 6, 1, 0, 0, 0, TimeSpan.Zero);

    public ConnectionRepositoryTests(DynamoDbFixture fixture)
    {
        _repo     = fixture.Services.GetRequiredService<IConnectionRepository>();
        _userRepo = fixture.Services.GetRequiredService<IUserRepository>();
    }

    /// Seeds a user in DynamoDB so connection FK-like lookups don't fail on missing users.
    private async Task SeedUserAsync(string userId)
    {
        var user = User.Create(UserId.From(userId), $"User {userId}", $"{userId}@example.com", Now);
        await _userRepo.SaveAsync(user, CancellationToken.None);
    }

    [Fact]
    public async Task SaveAsync_PendingConnection_GetAsync_ReturnsPendingConnection()
    {
        // Arrange
        var requesterId = UserId.From("conn-u-001");
        var targetId    = UserId.From("conn-u-002");
        await SeedUserAsync(requesterId.Value);
        await SeedUserAsync(targetId.Value);

        var connectionId = ConnectionId.From("conn-int-001");
        var connection   = Connection.CreatePending(connectionId, requesterId, targetId, "Let's connect", Now);

        // Act
        await _repo.SaveAsync(connection, CancellationToken.None);
        var retrieved = await _repo.GetAsync(requesterId, targetId, CancellationToken.None);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Id.Value.Should().Be("conn-int-001");
        retrieved.OwnerId.Should().Be(requesterId);
        retrieved.TargetId.Should().Be(targetId);
        retrieved.Status.Should().Be(ConnectionStatus.Pending);
    }

    [Fact]
    public async Task GetAsync_NonExistentConnection_ReturnsNull()
    {
        var result = await _repo.GetAsync(
            UserId.From("conn-u-999"),
            UserId.From("conn-u-998"),
            CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task SaveBothDirectionsAsync_AcceptedConnection_AreConnectedAsync_ReturnsTrue()
    {
        // Arrange
        var userA = UserId.From("conn-u-003");
        var userB = UserId.From("conn-u-004");
        await SeedUserAsync(userA.Value);
        await SeedUserAsync(userB.Value);

        var connId = ConnectionId.From("conn-int-002");
        var aToB   = Connection.CreatePending(connId, userA, userB, null, Now);
        aToB.Accept(Now.AddMinutes(5));

        // Mirror connection from B's perspective (create pending then accept — same lifecycle as AcceptConnectionRequestHandler)
        var bToA = Connection.CreatePending(ConnectionId.From("conn-int-002-mirror"), userB, userA, null, Now);
        bToA.Accept(Now.AddMinutes(5));

        // Act
        await _repo.SaveBothDirectionsAsync(aToB, bToA, CancellationToken.None);
        var connected = await _repo.AreConnectedAsync(userA, userB, CancellationToken.None);

        // Assert
        connected.Should().BeTrue();
    }

    [Fact]
    public async Task AreConnectedAsync_UnconnectedUsers_ReturnsFalse()
    {
        var result = await _repo.AreConnectedAsync(
            UserId.From("conn-u-990"),
            UserId.From("conn-u-991"),
            CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetAcceptedConnectionsAsync_ReturnsOnlyAccepted()
    {
        // Arrange — create one pending and one accepted connection for the same user
        var owner   = UserId.From("conn-u-005");
        var target1 = UserId.From("conn-u-006");
        var target2 = UserId.From("conn-u-007");
        await SeedUserAsync(owner.Value);
        await SeedUserAsync(target1.Value);
        await SeedUserAsync(target2.Value);

        var pending  = Connection.CreatePending(ConnectionId.From("conn-int-003"), owner, target1, null, Now);
        var accepted = Connection.CreatePending(ConnectionId.From("conn-int-004"), owner, target2, null, Now);
        accepted.Accept(Now.AddMinutes(5));

        await _repo.SaveAsync(pending,  CancellationToken.None);
        await _repo.SaveAsync(accepted, CancellationToken.None);

        // Act
        var results = await _repo.GetAcceptedConnectionsAsync(owner, CancellationToken.None);

        // Assert — only the accepted one is returned
        results.Should().Contain(c => c.Id.Value == "conn-int-004");
        results.Should().NotContain(c => c.Id.Value == "conn-int-003");
    }

    [Fact]
    public async Task DeleteBothDirectionsAsync_AcceptedConnection_AreConnectedAsync_ReturnsFalse()
    {
        // Arrange
        var userC = UserId.From("conn-u-008");
        var userD = UserId.From("conn-u-009");
        await SeedUserAsync(userC.Value);
        await SeedUserAsync(userD.Value);

        var cToD = Connection.CreatePending(ConnectionId.From("conn-int-005"), userC, userD, null, Now);
        cToD.Accept(Now.AddMinutes(1));
        var dToC = Connection.CreatePending(ConnectionId.From("conn-int-005-mirror"), userD, userC, null, Now);
        dToC.Accept(Now.AddMinutes(1));
        await _repo.SaveBothDirectionsAsync(cToD, dToC, CancellationToken.None);

        // Act
        await _repo.DeleteBothDirectionsAsync(userC, userD, CancellationToken.None);
        var connected = await _repo.AreConnectedAsync(userC, userD, CancellationToken.None);

        // Assert
        connected.Should().BeFalse();
    }
}
