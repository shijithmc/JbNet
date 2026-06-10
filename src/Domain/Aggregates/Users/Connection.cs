using JbNet.Domain.Enums;
using JbNet.Domain.ValueObjects;

namespace JbNet.Domain.Aggregates.Users;

/// <summary>
/// Represents a directed connection record in the adjacency list.
/// Two Connection records exist per accepted relationship: A→B and B→A.
/// </summary>
public sealed class Connection
{
    public ConnectionId Id { get; private set; }
    public UserId OwnerId { get; private set; }
    public UserId TargetId { get; private set; }
    public ConnectionStatus Status { get; private set; }
    public string? InviteNote { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private Connection() { }

    public static Connection CreatePending(
        ConnectionId id,
        UserId requesterId,
        UserId targetId,
        string? note,
        DateTimeOffset createdAt)
    {
        return new Connection
        {
            Id = id,
            OwnerId = requesterId,
            TargetId = targetId,
            Status = ConnectionStatus.Pending,
            InviteNote = note?.Trim(),
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };
    }

    public void Accept(DateTimeOffset updatedAt)
    {
        if (Status != ConnectionStatus.Pending)
            throw new InvalidOperationException($"Cannot accept a connection in {Status} state.");

        Status = ConnectionStatus.Accepted;
        UpdatedAt = updatedAt;
    }

    public void Decline(DateTimeOffset updatedAt)
    {
        if (Status != ConnectionStatus.Pending)
            throw new InvalidOperationException($"Cannot decline a connection in {Status} state.");

        Status = ConnectionStatus.Declined;
        UpdatedAt = updatedAt;
    }

    public void Remove(DateTimeOffset updatedAt)
    {
        Status = ConnectionStatus.Removed;
        UpdatedAt = updatedAt;
    }

    public bool IsAccepted => Status == ConnectionStatus.Accepted;
}
