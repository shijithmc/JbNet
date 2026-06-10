using MediatR;

namespace JbNet.Application.Connections.Commands.SendConnectionRequest;

/// <summary>Sends a connection request from requester to target user.</summary>
public sealed record SendConnectionRequestCommand(
    string RequesterId,
    string TargetUserId,
    string? Note
) : IRequest<SendConnectionRequestResult>;

public sealed record SendConnectionRequestResult(string ConnectionId);
