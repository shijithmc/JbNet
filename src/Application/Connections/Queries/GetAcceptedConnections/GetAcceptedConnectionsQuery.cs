using MediatR;

namespace JbNet.Application.Connections.Queries.GetAcceptedConnections;

/// <summary>Returns the list of accepted connections for the requesting user.</summary>
public sealed record GetAcceptedConnectionsQuery(string UserId)
    : IRequest<IReadOnlyList<ConnectionSummary>>;

/// <summary>Lightweight summary of a single accepted connection, suitable for list display.</summary>
public sealed record ConnectionSummary(
    string ConnectionId,
    string ConnectedUserId,
    DateTimeOffset AcceptedAt
);
