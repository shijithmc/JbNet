using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using JbNet.Domain.Aggregates.Users;
using JbNet.Domain.Repositories;
using JbNet.Domain.ValueObjects;
using JbNet.Infrastructure.DynamoDB.Mappers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JbNet.Infrastructure.DynamoDB.Repositories;

public sealed class DynamoUserRepository(
    IAmazonDynamoDB dynamoDb,
    IOptions<DynamoDbOptions> options,
    ILogger<DynamoUserRepository> logger) : IUserRepository
{
    private readonly string _tableName = options.Value.TableName;

    public async Task<User?> GetByIdAsync(UserId userId, CancellationToken ct)
    {
        var response = await dynamoDb.GetItemAsync(new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                [DynamoTableKeys.PkAttr] = new AttributeValue(DynamoTableKeys.UserPk(userId.Value)),
                [DynamoTableKeys.SkAttr] = new AttributeValue(DynamoTableKeys.UserProfileSk())
            }
        }, ct);

        return response.IsItemSet ? UserMapper.FromItem(response.Item) : null;
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct)
    {
        // Email is indexed via GSI2 in production. For v1 with small dataset, scan with filter.
        // [ASSUMED] GSI2 on email will be added when user volume exceeds 10k — scan acceptable at launch.
        var response = await dynamoDb.ScanAsync(new ScanRequest
        {
            TableName = _tableName,
            FilterExpression = "EntityType = :et AND Email = :email",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":et"] = new AttributeValue("User"),
                [":email"] = new AttributeValue(email.ToLowerInvariant())
            },
            Limit = 1
        }, ct);

        return response.Items.FirstOrDefault() is { } item ? UserMapper.FromItem(item) : null;
    }

    public async Task SaveAsync(User user, CancellationToken ct)
    {
        var item = UserMapper.ToItem(user);

        try
        {
            await dynamoDb.PutItemAsync(new PutItemRequest
            {
                TableName = _tableName,
                Item = item
            }, ct);
        }
        catch (ConditionalCheckFailedException ex)
        {
            logger.LogWarning(ex, "Conditional check failed saving user {UserId}", user.Id.Value);
            throw;
        }
    }

    public async Task<IReadOnlyList<User>> GetByIdsAsync(IReadOnlyList<UserId> userIds, CancellationToken ct)
    {
        if (userIds.Count == 0) return [];

        // BatchGetItem: max 100 per call; chunk if needed
        var keys = userIds.Select(id => new Dictionary<string, AttributeValue>
        {
            [DynamoTableKeys.PkAttr] = new AttributeValue(DynamoTableKeys.UserPk(id.Value)),
            [DynamoTableKeys.SkAttr] = new AttributeValue(DynamoTableKeys.UserProfileSk())
        }).ToList();

        var results = new List<User>();
        const int batchSize = 100;

        for (int i = 0; i < keys.Count; i += batchSize)
        {
            var batch = keys.Skip(i).Take(batchSize).ToList();
            var response = await dynamoDb.BatchGetItemAsync(new BatchGetItemRequest
            {
                RequestItems = new Dictionary<string, KeysAndAttributes>
                {
                    [_tableName] = new KeysAndAttributes { Keys = batch }
                }
            }, ct);

            if (response.Responses.TryGetValue(_tableName, out var items))
                results.AddRange(items.Select(UserMapper.FromItem));
        }

        return results.AsReadOnly();
    }

    public async Task<IReadOnlyList<User>> SearchByNameOrEmployerAsync(string query, int limit, CancellationToken ct)
    {
        // Full-text search not available in DynamoDB without OpenSearch.
        // [ASSUMED] Case-insensitive prefix scan on FullName for v1 (acceptable at low user counts).
        // v2: integrate OpenSearch or Meilisearch for proper search.
        var response = await dynamoDb.ScanAsync(new ScanRequest
        {
            TableName = _tableName,
            FilterExpression = "EntityType = :et AND (contains(FullNameLower, :q) OR contains(EmployerNameLower, :q))",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":et"] = new AttributeValue("User"),
                [":q"] = new AttributeValue(query.ToLowerInvariant())
            },
            Limit = limit * 5 // over-fetch to compensate for filter efficiency
        }, ct);

        return response.Items
            .Take(limit)
            .Select(UserMapper.FromItem)
            .ToList()
            .AsReadOnly();
    }
}
