using JbNet.Application.Users.Commands.SetResume;
using JbNet.Application.Users.Commands.UpdateUserProfile;
using JbNet.Application.Users.Queries.GetUserProfile;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JbNet.Api.Controllers;

[ApiController]
[Route("users")]
[Authorize]
public sealed class UsersController(ISender sender) : ControllerBase
{
    /// <summary>Returns the authenticated user's profile.</summary>
    [HttpGet("me")]
    public async Task<IActionResult> GetMe(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await sender.Send(new GetUserProfileQuery(userId), ct);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Updates the authenticated user's profile.</summary>
    [HttpPut("me")]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateProfileRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await sender.Send(new UpdateUserProfileCommand(
            userId,
            request.FullName,
            request.Headline,
            request.EmployerName,
            request.City), ct);
        return Ok(result);
    }

    /// <summary>
    /// Initiates a resume upload. Returns a presigned S3 URL — client uploads the PDF directly to S3.
    /// After S3 upload completes, no additional confirmation call is needed; the URL generation records the S3 key.
    /// </summary>
    [HttpPost("me/resume")]
    public async Task<IActionResult> SetResume([FromBody] SetResumeRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await sender.Send(new SetResumeCommand(userId, request.FileName, request.SizeBytes), ct);
        return Ok(result);
    }

    private string GetCurrentUserId() =>
        User.FindFirst("sub")?.Value
        ?? throw new UnauthorizedAccessException("JWT sub claim missing.");
}

public sealed record UpdateProfileRequest(
    string FullName,
    string Headline,
    string? EmployerName,
    string? City);

public sealed record SetResumeRequest(
    string FileName,
    long SizeBytes);
