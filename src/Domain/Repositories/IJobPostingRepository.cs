using JbNet.Domain.Aggregates.Jobs;
using JbNet.Domain.ValueObjects;

namespace JbNet.Domain.Repositories;

/// <summary>Persistence interface for JobPosting aggregate.</summary>
public interface IJobPostingRepository
{
    Task<JobPosting?> GetByIdAsync(JobId jobId, CancellationToken ct);
    Task SaveAsync(JobPosting job, CancellationToken ct);

    /// <summary>Returns paginated active (non-expired) postings ordered by PostedAt descending.</summary>
    Task<(IReadOnlyList<JobPosting> Items, string? NextPageToken)> ListActiveAsync(
        int limit,
        string? pageToken,
        CancellationToken ct);

    Task<IReadOnlyList<JobPosting>> SearchAsync(string query, int limit, CancellationToken ct);

    /// <summary>Returns all active postings for a specific company (used by path discovery).</summary>
    Task<IReadOnlyList<JobPosting>> GetByCompanyNameAsync(string normalisedCompanyName, CancellationToken ct);
}
