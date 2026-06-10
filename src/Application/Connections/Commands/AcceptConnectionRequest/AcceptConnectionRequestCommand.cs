using MediatR;

namespace JbNet.Application.Connections.Commands.AcceptConnectionRequest;

/// <summary>Accepts a pending connection request. Creates bidirectional connection records.</summary>
public sealed record AcceptConnectionRequestCommand(
    string AccepterId,
    string RequesterId
) : IRequest<AcceptConnectionRequestResult>;

public sealed record AcceptConnectionRequestResult(string Status);
