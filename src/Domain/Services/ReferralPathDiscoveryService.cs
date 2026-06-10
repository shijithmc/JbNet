using JbNet.Domain.Aggregates.Users;
using JbNet.Domain.Repositories;
using JbNet.Domain.ValueObjects;

namespace JbNet.Domain.Services;

/// <summary>
/// Domain service for 2-hop BFS path discovery.
/// Finds all paths: JobSeeker → 1st-degree connection → employee at target company.
/// Deduplicates circular references. Returns paths ordered by hop count (1-hop first), then connection recency.
/// </summary>
public sealed class ReferralPathDiscoveryService(
    IConnectionRepository connectionRepository,
    IUserRepository userRepository)
{
    /// <param name="jobSeekerId">The user requesting referral paths.</param>
    /// <param name="normalisedCompanyName">Lowercase company name from JobPosting. Matched case-insensitively against user.EmployerName.</param>
    /// <param name="maxHops">Maximum hop depth. v1 = 2.</param>
    public async Task<IReadOnlyList<ReferralPath>> DiscoverPathsAsync(
        UserId jobSeekerId,
        string normalisedCompanyName,
        int maxHops,
        CancellationToken ct)
    {
        var paths = new List<ReferralPath>();

        // BFS level 1: job seeker's 1st-degree connections
        var firstDegree = await connectionRepository.GetAcceptedConnectionsAsync(jobSeekerId, ct);
        var firstDegreeIds = firstDegree.Select(c => c.TargetId).ToList();
        var firstDegreeUsers = await userRepository.GetByIdsAsync(firstDegreeIds, ct);

        var firstDegreeByEmployer = firstDegreeUsers
            .Where(u => u.EmployerName != null &&
                        u.EmployerName.Trim().ToLowerInvariant() == normalisedCompanyName)
            .ToList();

        // 1-hop paths: job seeker's direct connection works at target company
        foreach (var directEmployee in firstDegreeByEmployer)
        {
            paths.Add(new ReferralPath(
                Hops: [new ReferralPathHop(directEmployee.Id, directEmployee.FullName, directEmployee.Headline, directEmployee.EmployerName!, true)],
                TotalHops: 1));
        }

        if (maxHops < 2) return paths.AsReadOnly();

        // BFS level 2: 2nd-degree connections
        // For efficiency: only traverse 1st-degree connections who are NOT themselves at target company
        var intermediaries = firstDegreeUsers
            .Where(u => u.EmployerName == null ||
                        u.EmployerName.Trim().ToLowerInvariant() != normalisedCompanyName)
            .ToList();

        foreach (var intermediary in intermediaries)
        {
            ct.ThrowIfCancellationRequested();

            var secondDegree = await connectionRepository.GetAcceptedConnectionsAsync(intermediary.Id, ct);

            // Exclude the job seeker themselves (prevent circular A→B→A)
            var secondDegreeIds = secondDegree
                .Select(c => c.TargetId)
                .Where(id => id != jobSeekerId)
                .Distinct()
                .ToList();

            var secondDegreeUsers = await userRepository.GetByIdsAsync(secondDegreeIds, ct);

            var targetEmployees = secondDegreeUsers
                .Where(u => u.EmployerName != null &&
                            u.EmployerName.Trim().ToLowerInvariant() == normalisedCompanyName)
                .ToList();

            foreach (var employee in targetEmployees)
            {
                // Deduplicate: skip if this employee was already found via a 1-hop path
                if (paths.Any(p => p.Hops[^1].UserId == employee.Id)) continue;

                paths.Add(new ReferralPath(
                    Hops: [
                        new ReferralPathHop(intermediary.Id, intermediary.FullName, intermediary.Headline, intermediary.EmployerName ?? string.Empty, false),
                        new ReferralPathHop(employee.Id, employee.FullName, employee.Headline, employee.EmployerName!, true)
                    ],
                    TotalHops: 2));
            }
        }

        // Sort: 1-hop before 2-hop; ties broken by final employee's employer alpha order
        return paths.OrderBy(p => p.TotalHops).ThenBy(p => p.Hops[^1].FullName).ToList().AsReadOnly();
    }
}

/// <summary>A discovered referral path showing the chain from job seeker to target company employee.</summary>
public sealed record ReferralPath(IReadOnlyList<ReferralPathHop> Hops, int TotalHops);

/// <summary>One participant in a referral path.</summary>
public sealed record ReferralPathHop(
    UserId UserId,
    string FullName,
    string Headline,
    string EmployerName,
    bool IsAtTargetCompany);
