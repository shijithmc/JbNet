using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using JbNet.Domain.Aggregates.Jobs;
using JbNet.Domain.Repositories;
using JbNet.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JbNet.Infrastructure.DynamoDB.Repositories;

public sealed class DynamoJobPostingRepository(
    IAmazonDynamoDB dynamoDb,
    IOptions<DynamoDbOptions> options,
    ILogger<DynamoJobPostingRepository> logger) : IJobPostingRepository
{
    private readonly string _tableName = options.Value.TableName;

    public async Task<JobPosting?> GetByIdAsync(JobId jobId, CancellationToken ct)
    {
        var response = await dynamoDb.GetItemAsync(new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                [DynamoTableKeys.PkAttr] = new(DynamoTableKeys.JobPk(jobId.Value)),
                [DynamoTableKeys.SkAttr] = new(DynamoTableKeys.JobMetadataSk())
            }
        }, ct);

        return response.IsItemSet ? MapFromItem(response.Item) : null;
    }

    public async Task SaveAsync(JobPosting job, CancellationToken ct)
    {
        var transact = new TransactWriteItemsRequest
        {
            TransactItems =
            [
                // Primary item: JOB#<id> / METADATA
                new TransactWriteItem { Put = new Put { TableName = _tableName, Item = MapToItem(job) } },
                // Feed item: PARTITION#JOBS / DATE#<date>#<id> — for paginated browse
                new TransactWriteItem { Put = new Put { TableName = _tableName, Item = MapToFeedItem(job) } }
            ]
        };

        await dynamoDb.TransactWriteItemsAsync(transact, ct);
    }

    public async Task<(IReadOnlyList<JobPosting> Items, string? NextPageToken)> ListActiveAsync(
        int limit, string? pageToken, CancellationToken ct)
    {
        var request = new QueryRequest
        {
            TableName = _tableName,
            KeyConditionExpression = "PK = :pk",
            FilterExpression = "IsActive = :active",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"]     = new(DynamoTableKeys.JobFeedPk()),
                [":active"] = new AttributeValue { BOOL = true }
            },
            ScanIndexForward = false, // newest first
            Limit = limit
        };

        if (pageToken != null)
        {
            request.ExclusiveStartKey = new Dictionary<string, AttributeValue>
            {
                [DynamoTableKeys.PkAttr] = new(DynamoTableKeys.JobFeedPk()),
                [DynamoTableKeys.SkAttr] = new(pageToken)
            };
        }

        var response = await dynamoDb.QueryAsync(request, ct);

        // Resolve full job items from feed item SK references
        var jobIds = response.Items
            .Select(i => JobId.From(i["JobId"].S))
            .ToList();

        var jobs = await GetManyByIdAsync(jobIds, ct);

        string? nextToken = response.LastEvaluatedKey?.TryGetValue(DynamoTableKeys.SkAttr, out var lastSk) == true
            ? lastSk.S : null;

        return (jobs, nextToken);
    }

    public async Task<IReadOnlyList<JobPosting>> SearchAsync(string query, int limit, CancellationToken ct)
    {
        // [ASSUMED] Contains-based scan for v1. Replace with OpenSearch in v2 for proper full-text.
        var response = await dynamoDb.ScanAsync(new ScanRequest
        {
            TableName = _tableName,
            FilterExpression = "EntityType = :et AND IsActive = :active AND (contains(CompanyNameLower, :q) OR contains(TitleLower, :q))",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":et"]     = new("JobPosting"),
                [":active"] = new AttributeValue { BOOL = true },
                [":q"]      = new(query.ToLowerInvariant())
            },
            Limit = limit * 5
        }, ct);

        return response.Items.Take(limit).Select(MapFromItem).ToList().AsReadOnly();
    }

    public async Task<IReadOnlyList<JobPosting>> GetByCompanyNameAsync(string normalisedCompanyName, CancellationToken ct)
    {
        var response = await dynamoDb.ScanAsync(new ScanRequest
        {
            TableName = _tableName,
            FilterExpression = "EntityType = :et AND IsActive = :active AND CompanyNameLower = :company",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":et"]      = new("JobPosting"),
                [":active"]  = new AttributeValue { BOOL = true },
                [":company"] = new(normalisedCompanyName)
            }
        }, ct);

        return response.Items.Select(MapFromItem).ToList().AsReadOnly();
    }

    private async Task<IReadOnlyList<JobPosting>> GetManyByIdAsync(IReadOnlyList<JobId> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return [];

        var keys = ids.Select(id => new Dictionary<string, AttributeValue>
        {
            [DynamoTableKeys.PkAttr] = new(DynamoTableKeys.JobPk(id.Value)),
            [DynamoTableKeys.SkAttr] = new(DynamoTableKeys.JobMetadataSk())
        }).ToList();

        var response = await dynamoDb.BatchGetItemAsync(new BatchGetItemRequest
        {
            RequestItems = new Dictionary<string, KeysAndAttributes>
            {
                [_tableName] = new KeysAndAttributes { Keys = keys }
            }
        }, ct);

        return response.Responses.TryGetValue(_tableName, out var items)
            ? items.Select(MapFromItem).ToList().AsReadOnly()
            : [];
    }

    private static Dictionary<string, AttributeValue> MapToItem(JobPosting job) =>
        new()
        {
            [DynamoTableKeys.PkAttr]         = new(DynamoTableKeys.JobPk(job.Id.Value)),
            [DynamoTableKeys.SkAttr]         = new(DynamoTableKeys.JobMetadataSk()),
            [DynamoTableKeys.EntityTypeAttr] = new("JobPosting"),
            ["JobId"]            = new(job.Id.Value),
            ["CompanyName"]      = new(job.CompanyName),
            ["CompanyNameLower"] = new(job.NormalisedCompanyName),
            ["Title"]            = new(job.Title),
            ["TitleLower"]       = new(job.Title.ToLowerInvariant()),
            ["Description"]      = new(job.Description),
            ["Location"]         = new(job.Location),
            ["IsActive"]         = new AttributeValue { BOOL = job.IsActive },
            ["PostedAt"]         = new(job.PostedAt.ToString("O")),
            ["ExpiresAt"]        = new(job.ExpiresAt.ToString("O")),
            [DynamoTableKeys.TtlAttr] = new AttributeValue
                { N = job.ExpiresAt.ToUnixTimeSeconds().ToString() }
        };

    private static Dictionary<string, AttributeValue> MapToFeedItem(JobPosting job) =>
        new()
        {
            [DynamoTableKeys.PkAttr] = new(DynamoTableKeys.JobFeedPk()),
            [DynamoTableKeys.SkAttr] = new(DynamoTableKeys.JobFeedSk(job.PostedAt, job.Id.Value)),
            ["JobId"]                = new(job.Id.Value),
            ["IsActive"]             = new AttributeValue { BOOL = job.IsActive },
            [DynamoTableKeys.TtlAttr] = new AttributeValue
                { N = job.ExpiresAt.ToUnixTimeSeconds().ToString() }
        };

    private static JobPosting MapFromItem(Dictionary<string, AttributeValue> item)
    {
        var jobId = JobId.From(item["JobId"].S);
        var postedAt = DateTimeOffset.Parse(item["PostedAt"].S);

        return JobPosting.Create(
            jobId,
            item["CompanyName"].S,
            item["Title"].S,
            item.TryGetValue("Description", out var desc) ? desc.S : string.Empty,
            item.TryGetValue("Location", out var loc) ? loc.S : string.Empty,
            null,
            postedAt);
    }
}
