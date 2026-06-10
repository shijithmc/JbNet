using JbNet.Application.Jobs.Queries.ListJobPostings;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JbNet.Api.Controllers;

[ApiController]
[Route("jobs")]
[Authorize]
public sealed class JobsController(ISender sender) : ControllerBase
{
    /// <summary>Returns a paginated list of active job postings, newest first. Optionally filters by search query.</summary>
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int limit = 20,
        [FromQuery] string? pageToken = null,
        [FromQuery] string? q = null,
        CancellationToken ct = default)
    {
        var result = await sender.Send(new ListJobPostingsQuery(limit, pageToken, q), ct);
        return Ok(result);
    }
}
