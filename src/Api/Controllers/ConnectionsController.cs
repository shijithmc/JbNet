using JbNet.Application.Connections.Commands.AcceptConnectionRequest;
using JbNet.Application.Connections.Commands.SendConnectionRequest;
using JbNet.Application.Connections.Queries.GetAcceptedConnections;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JbNet.Api.Controllers;

[ApiController]
[Route("connections")]
[Authorize]
public sealed class ConnectionsController(ISender sender) : ControllerBase
{
    /// <summary>Returns the authenticated user's accepted connections.</summary>
    [HttpGet("me")]
    public async Task<IActionResult> GetMyConnections(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await sender.Send(new GetAcceptedConnectionsQuery(userId), ct);
        return Ok(result);
    }

    /// <summary>Send a connection request to another user.</summary>
    [HttpPost]
    public async Task<IActionResult> Send([FromBody] SendConnectionRequestBody request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await sender.Send(new SendConnectionRequestCommand(userId, request.TargetUserId, request.Note), ct);
        return CreatedAtAction(null, new { id = result.ConnectionId }, result);
    }

    /// <summary>Accept a pending connection request from the specified user.</summary>
    [HttpPost("{requesterId}/accept")]
    public async Task<IActionResult> Accept(string requesterId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await sender.Send(new AcceptConnectionRequestCommand(userId, requesterId), ct);
        return Ok(result);
    }

    private string GetCurrentUserId() =>
        User.FindFirst("sub")?.Value
        ?? throw new UnauthorizedAccessException("JWT sub claim missing.");
}

public sealed record SendConnectionRequestBody(string TargetUserId, string? Note);
