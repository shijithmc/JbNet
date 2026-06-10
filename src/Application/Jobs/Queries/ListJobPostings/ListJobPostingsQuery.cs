using MediatR;

namespace JbNet.Application.Jobs.Queries.ListJobPostings;

/// <summary>Returns a paginated list of active job postings, ordered by PostedAt descending.</summary>
public sealed record ListJobPostingsQuery(
    int Limit = 20,
    string? PageToken = null,
    string? SearchQuery = null
) : IRequest<ListJobPostingsResult>;

public sealed record ListJobPostingsResult(
    IReadOnlyList<JobPostingDto> Items,
    string? NextPageToken,
    int TotalReturned
);

public sealed record JobPostingDto(
    string Id,
    string CompanyName,
    string Title,
    string Location,
    DateTimeOffset PostedAt,
    int DaysAgo
);
