using JbNet.Domain.Repositories;
using MediatR;

namespace JbNet.Application.Jobs.Queries.ListJobPostings;

public sealed class ListJobPostingsHandler(
    IJobPostingRepository jobRepository) : IRequestHandler<ListJobPostingsQuery, ListJobPostingsResult>
{
    public async Task<ListJobPostingsResult> Handle(ListJobPostingsQuery query, CancellationToken ct)
    {
        var limit = Math.Clamp(query.Limit, 1, 50);
        var now = DateTimeOffset.UtcNow;

        var (items, nextPageToken) = string.IsNullOrWhiteSpace(query.SearchQuery)
            ? await jobRepository.ListActiveAsync(limit, query.PageToken, ct)
            : (await jobRepository.SearchAsync(query.SearchQuery.Trim(), limit, ct), null);

        var dtos = items.Select(j => new JobPostingDto(
            j.Id.Value,
            j.CompanyName,
            j.Title,
            j.Location,
            j.PostedAt,
            (int)(now - j.PostedAt).TotalDays)).ToList();

        return new ListJobPostingsResult(dtos, nextPageToken, dtos.Count);
    }
}
