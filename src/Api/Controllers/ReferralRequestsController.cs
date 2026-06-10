using JbNet.Application.Referrals.Commands.AcceptReferralRequest;
using JbNet.Application.Referrals.Commands.CreateReferralRequest;
using JbNet.Application.Referrals.Commands.DeclineReferralRequest;
using JbNet.Application.Referrals.Commands.ForwardReferralRequest;
using JbNet.Application.Referrals.Commands.WithdrawReferralRequest;
using JbNet.Application.Referrals.Queries.DiscoverReferralPaths;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JbNet.Api.Controllers;

[ApiController]
[Route("referrals")]
[Authorize]
public sealed class ReferralRequestsController(ISender sender) : ControllerBase
{
    /// <summary>Discover referral paths from the authenticated user to employees at the posting's company. Max 2 hops.</summary>
    [HttpGet("paths")]
    public async Task<IActionResult> DiscoverPaths([FromQuery] string jobId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await sender.Send(new DiscoverReferralPathsQuery(userId, jobId), ct);
        return Ok(result);
    }

    /// <summary>Create a referral request along the selected path.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateReferralRequestRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await sender.Send(new CreateReferralRequestCommand(
            userId,
            request.JobId,
            request.HopParticipantIds,
            request.PersonalNote), ct);
        return CreatedAtAction(nameof(GetStatus), new { id = result.RequestId }, result);
    }

    /// <summary>Get referral request status. Placeholder for future GetById query.</summary>
    [HttpGet("{id}")]
    public IActionResult GetStatus(string id) => Ok(new { id });

    /// <summary>Forward a referral request to the next hop.</summary>
    [HttpPost("{id}/forward")]
    public async Task<IActionResult> Forward(string id, [FromBody] ForwardReferralRequestRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await sender.Send(new ForwardReferralRequestCommand(id, userId, request.Note), ct);
        return Ok(result);
    }

    /// <summary>Final referrer accepts — commits to internally referring the candidate.</summary>
    [HttpPost("{id}/accept")]
    public async Task<IActionResult> Accept(string id, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await sender.Send(new AcceptReferralRequestCommand(id, userId), ct);
        return Ok(result);
    }

    /// <summary>Decline a referral request at the current hop.</summary>
    [HttpPost("{id}/decline")]
    public async Task<IActionResult> Decline(string id, [FromBody] DeclineReferralRequestRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await sender.Send(new DeclineReferralRequestCommand(id, userId), ct);
        return Ok(result);
    }

    /// <summary>Job seeker withdraws their own request.</summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Withdraw(string id, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await sender.Send(new WithdrawReferralRequestCommand(id, userId), ct);
        return Ok(result);
    }

    private string GetCurrentUserId() =>
        User.FindFirst("sub")?.Value
        ?? throw new UnauthorizedAccessException("JWT sub claim missing.");
}

public sealed record CreateReferralRequestRequest(
    string JobId,
    IReadOnlyList<string> HopParticipantIds,
    string? PersonalNote);

public sealed record ForwardReferralRequestRequest(string? Note);
public sealed record DeclineReferralRequestRequest(string? Reason);
