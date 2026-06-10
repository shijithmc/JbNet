using JbNet.Domain.Aggregates.Users;
using JbNet.Domain.ValueObjects;

namespace JbNet.Domain.Repositories;

/// <summary>Persistence interface for the User aggregate. Implemented in Infrastructure (DynamoDB).</summary>
public interface IUserRepository
{
    Task<User?> GetByIdAsync(UserId userId, CancellationToken ct);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct);
    Task SaveAsync(User user, CancellationToken ct);
    Task<IReadOnlyList<User>> GetByIdsAsync(IReadOnlyList<UserId> userIds, CancellationToken ct);
    Task<IReadOnlyList<User>> SearchByNameOrEmployerAsync(string query, int limit, CancellationToken ct);
}
