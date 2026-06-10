using JbNet.Domain.Repositories;
using JbNet.Domain.ValueObjects;
using MediatR;

namespace JbNet.Application.Connections.Queries.GetAcceptedConnections;

/// <summary>
/// Retrieves all accepted connections for the requesting user from the repository.
/// Returns them as a flat list ordered by accepted time descending.
/// </summary>
public sealed class GetAcceptedConnectionsHandler(IConnectionRepository connectionRepository)
    : IRequestHandler<GetAcceptedConnectionsQuery, IReadOnlyList<ConnectionSummary>>
{
    public async Task<IReadOnlyList<ConnectionSummary>> Handle(
        GetAcceptedConnectionsQuery request,
        CancellationToken cancellationToken)
    {
        var userId      = UserId.From(request.UserId);
        var connections = await connectionRepository.GetAcceptedConnectionsAsync(
            userId, cancellationToken);

        return connections
            .Select(c => new ConnectionSummary(
                ConnectionId:    c.Id.Value,
                ConnectedUserId: c.TargetId.Value,
                AcceptedAt:      c.UpdatedAt))
            .OrderByDescending(c => c.AcceptedAt)
            .ToList()
            .AsReadOnly();
    }
}
