using JbNet.Domain.Aggregates.Users;
using JbNet.Domain.ValueObjects;

namespace JbNet.Domain.Repositories;

/// <summary>Persistence interface for Connection records (adjacency list).</summary>
public interface IConnectionRepository
{
    Task<Connection?> GetAsync(UserId ownerId, UserId targetId, CancellationToken ct);
    Task SaveAsync(Connection connection, CancellationToken ct);
    Task SaveBothDirectionsAsync(Connection requesterRecord, Connection targetRecord, CancellationToken ct);
    Task<IReadOnlyList<Connection>> GetAcceptedConnectionsAsync(UserId userId, CancellationToken ct);
    Task DeleteBothDirectionsAsync(UserId userA, UserId userB, CancellationToken ct);
    Task<bool> AreConnectedAsync(UserId userA, UserId userB, CancellationToken ct);
}
