using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using JbNet.Domain.Aggregates.Referrals;
using JbNet.Domain.Enums;
using JbNet.Domain.Repositories;
using JbNet.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JbNet.Infrastructure.DynamoDB.Repositories;

public sealed class DynamoReferralRequestRepository(
    IAmazonDynamoDB dynamoDb,
    IOptions<DynamoDbOptions> options,
    ILogger<DynamoReferralRequestRepository> logger) : IReferralRequestRepository
{
    private readonly string _tableName = options.Value.TableName;

    public async Task<ReferralRequest?> GetByIdAsync(RequestId requestId, CancellationToken ct)
    {
        var response = await dynamoDb.GetItemAsync(new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                [DynamoTableKeys.PkAttr] = new(DynamoTableKeys.ReferralPk(requestId.Value)),
                [DynamoTableKeys.SkAttr] = new(DynamoTableKeys.ReferralMetadataSk())
            }
        }, ct);

        return response.IsItemSet ? MapFromItem(response.Item) : null;
    }

    public async Task SaveAsync(ReferralRequest request, CancellationToken ct)
    {
        var writes = new List<TransactWriteItem>
        {
            // Primary record
            new() { Put = new Put { TableName = _tableName, Item = MapToItem(request) } },
            // Seeker index record (USER#<seekerId> / REF#<requestId>)
            new() { Put = new Put { TableName = _tableName, Item = MapToSeekerIndexItem(request) } }
        };

        // GSI1 record for current pending participant — updated on every state change
        if (request.IsActive && request.Hops.Count > request.CurrentHopIndex)
        {
            var currentParticipant = request.Hops[request.CurrentHopIndex].ParticipantId;
            writes.Add(new TransactWriteItem
            {
                Put = new Put { TableName = _tableName, Item = MapToGsi1Item(request, currentParticipant) }
            });
        }

        try
        {
            await dynamoDb.TransactWriteItemsAsync(
                new TransactWriteItemsRequest { TransactItems = writes }, ct);
        }
        catch (ConditionalCheckFailedException ex)
        {
            logger.LogWarning(ex, "Conditional check failed saving referral request {RequestId}", request.Id.Value);
            throw;
        }
    }

    public async Task<IReadOnlyList<ReferralRequest>> GetActiveByJobSeekerAsync(UserId jobSeekerId, CancellationToken ct)
    {
        var response = await dynamoDb.QueryAsync(new QueryRequest
        {
            TableName = _tableName,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :prefix)",
            FilterExpression = "ReferralStatus IN (:s1, :s2, :s3)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"]     = new(DynamoTableKeys.UserPk(jobSeekerId.Value)),
                [":prefix"] = new("REF#"),
                [":s1"]     = new(ReferralStatus.Sent.ToString()),
                [":s2"]     = new(ReferralStatus.Forwarded.ToString()),
                [":s3"]     = new(ReferralStatus.ReachedFinalReferrer.ToString())
            }
        }, ct);

        // Seeker index items only contain RequestId — resolve full records
        var requestIds = response.Items
            .Select(i => RequestId.From(i["RequestId"].S))
            .ToList();

        return await GetManyByIdsAsync(requestIds, ct);
    }

    public async Task<IReadOnlyList<ReferralRequest>> GetPendingByParticipantAsync(UserId participantId, CancellationToken ct)
    {
        var response = await dynamoDb.QueryAsync(new QueryRequest
        {
            TableName = _tableName,
            IndexName = DynamoTableKeys.Gsi1IndexName,
            KeyConditionExpression = "GSI1PK = :pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new(DynamoTableKeys.Gsi1Pk(participantId.Value))
            },
            ScanIndexForward = false
        }, ct);

        var requestIds = response.Items
            .Select(i => RequestId.From(i["RequestId"].S))
            .ToList();

        return await GetManyByIdsAsync(requestIds, ct);
    }

    public async Task<ReferralCooldown?> GetCooldownAsync(UserId userId, JobId jobId, CancellationToken ct)
    {
        var response = await dynamoDb.GetItemAsync(new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                [DynamoTableKeys.PkAttr] = new(DynamoTableKeys.UserPk(userId.Value)),
                [DynamoTableKeys.SkAttr] = new(DynamoTableKeys.UserCooldownSk(jobId.Value))
            }
        }, ct);

        if (!response.IsItemSet) return null;

        var item = response.Item;
        var createdAt = DateTimeOffset.Parse(item["CreatedAt"].S);
        return ReferralCooldown.Create(userId, jobId, createdAt);
    }

    public async Task SaveCooldownAsync(ReferralCooldown cooldown, CancellationToken ct)
    {
        await dynamoDb.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = new Dictionary<string, AttributeValue>
            {
                [DynamoTableKeys.PkAttr]         = new(DynamoTableKeys.UserPk(cooldown.UserId.Value)),
                [DynamoTableKeys.SkAttr]         = new(DynamoTableKeys.UserCooldownSk(cooldown.JobId.Value)),
                [DynamoTableKeys.EntityTypeAttr] = new("Cooldown"),
                ["UserId"]    = new(cooldown.UserId.Value),
                ["JobId"]     = new(cooldown.JobId.Value),
                ["CreatedAt"] = new(cooldown.CreatedAt.ToString("O")),
                ["ExpiresAt"] = new(cooldown.ExpiresAt.ToString("O")),
                [DynamoTableKeys.TtlAttr] = new AttributeValue
                    { N = cooldown.ExpiresAt.ToUnixTimeSeconds().ToString() }
            }
        }, ct);
    }

    public async Task<IReadOnlyList<ReferralRequest>> GetExpiredCandidatesAsync(int olderThanDays, CancellationToken ct)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-olderThanDays);
        var response = await dynamoDb.ScanAsync(new ScanRequest
        {
            TableName = _tableName,
            FilterExpression = "EntityType = :et AND ReferralStatus IN (:s1, :s2, :s3) AND UpdatedAt <= :cutoff",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":et"]     = new("ReferralRequest"),
                [":s1"]     = new(ReferralStatus.Sent.ToString()),
                [":s2"]     = new(ReferralStatus.Forwarded.ToString()),
                [":s3"]     = new(ReferralStatus.ReachedFinalReferrer.ToString()),
                [":cutoff"] = new(cutoff.ToString("O"))
            }
        }, ct);

        return response.Items.Select(MapFromItem).ToList().AsReadOnly();
    }

    private async Task<IReadOnlyList<ReferralRequest>> GetManyByIdsAsync(
        IReadOnlyList<RequestId> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return [];

        var keys = ids.Select(id => new Dictionary<string, AttributeValue>
        {
            [DynamoTableKeys.PkAttr] = new(DynamoTableKeys.ReferralPk(id.Value)),
            [DynamoTableKeys.SkAttr] = new(DynamoTableKeys.ReferralMetadataSk())
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

    private static Dictionary<string, AttributeValue> MapToItem(ReferralRequest request)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            [DynamoTableKeys.PkAttr]         = new(DynamoTableKeys.ReferralPk(request.Id.Value)),
            [DynamoTableKeys.SkAttr]         = new(DynamoTableKeys.ReferralMetadataSk()),
            [DynamoTableKeys.EntityTypeAttr] = new("ReferralRequest"),
            ["RequestId"]       = new(request.Id.Value),
            ["JobSeekerId"]     = new(request.JobSeekerId.Value),
            ["JobId"]           = new(request.JobId.Value),
            ["CompanyName"]     = new(request.CompanyName),
            ["JobTitle"]        = new(request.JobTitle),
            ["ResumeS3Key"]     = new(request.ResumeS3Key),
            ["ReferralStatus"]  = new(request.Status.ToString()),
            ["CurrentHopIndex"] = new AttributeValue { N = request.CurrentHopIndex.ToString() },
            ["CreatedAt"]       = new(request.CreatedAt.ToString("O")),
            ["ExpiresAt"]       = new(request.ExpiresAt.ToString("O")),
            ["UpdatedAt"]       = new(request.UpdatedAt.ToString("O")),
            [DynamoTableKeys.TtlAttr] = new AttributeValue
                { N = (request.ExpiresAt.ToUnixTimeSeconds() + 86400).ToString() } // +1 day grace
        };

        if (request.PersonalNote != null) item["PersonalNote"] = new(request.PersonalNote);

        // Embed hops as a JSON list attribute
        var hopsJson = System.Text.Json.JsonSerializer.Serialize(request.Hops.Select(h => new
        {
            Index = h.Index,
            ParticipantId = h.ParticipantId.Value,
            Status = h.Status.ToString(),
            ForwardNote = h.ForwardNote,
            CreatedAt = h.CreatedAt.ToString("O"),
            ActionTakenAt = h.ActionTakenAt?.ToString("O")
        }));
        item["Hops"] = new(hopsJson);

        return item;
    }

    private static Dictionary<string, AttributeValue> MapToSeekerIndexItem(ReferralRequest request) =>
        new()
        {
            [DynamoTableKeys.PkAttr] = new(DynamoTableKeys.UserPk(request.JobSeekerId.Value)),
            [DynamoTableKeys.SkAttr] = new(DynamoTableKeys.UserRefSk(request.Id.Value)),
            ["RequestId"]           = new(request.Id.Value),
            ["ReferralStatus"]      = new(request.Status.ToString())
        };

    private static Dictionary<string, AttributeValue> MapToGsi1Item(ReferralRequest request, UserId participantId) =>
        new()
        {
            [DynamoTableKeys.PkAttr]      = new(DynamoTableKeys.ReferralPk(request.Id.Value)),
            [DynamoTableKeys.SkAttr]      = new($"GSI1#{participantId.Value}"),
            [DynamoTableKeys.Gsi1PkAttr]  = new(DynamoTableKeys.Gsi1Pk(participantId.Value)),
            [DynamoTableKeys.Gsi1SkAttr]  = new(DynamoTableKeys.Gsi1Sk(request.CreatedAt)),
            ["RequestId"]                 = new(request.Id.Value),
            ["ReferralStatus"]            = new(request.Status.ToString())
        };

    private static ReferralRequest MapFromItem(Dictionary<string, AttributeValue> item)
    {
        var requestId = RequestId.From(item["RequestId"].S);
        var jobSeekerId = UserId.From(item["JobSeekerId"].S);
        var jobId = JobId.From(item["JobId"].S);
        var createdAt = DateTimeOffset.Parse(item["CreatedAt"].S);

        var hopsRaw = System.Text.Json.JsonSerializer.Deserialize<HopDto[]>(item["Hops"].S)!;
        var hopParticipants = hopsRaw.Select(h => UserId.From(h.ParticipantId)).ToList();

        var request = ReferralRequest.Create(
            requestId, jobSeekerId, jobId,
            item["CompanyName"].S, item["JobTitle"].S,
            item["ResumeS3Key"].S,
            item.TryGetValue("PersonalNote", out var note) ? note.S : null,
            hopParticipants, createdAt);

        // Restore state by replaying status from persisted value
        // (Domain events are not re-raised on hydration — we just set the status field directly)
        // Using the persisted status string to reconstruct state is intentional here.
        var statusStr = item["ReferralStatus"].S;
        if (Enum.TryParse<ReferralStatus>(statusStr, out var status))
        {
            // Replay hops to restore hop states
            foreach (var hopDto in hopsRaw)
            {
                var hop = request.Hops.FirstOrDefault(h => h.Index == hopDto.Index);
                if (hop == null) continue;
                var hopActionAt = hopDto.ActionTakenAt != null ? DateTimeOffset.Parse(hopDto.ActionTakenAt) : createdAt;
                var hopStatus = Enum.Parse<HopStatus>(hopDto.Status);
                if (hopStatus == HopStatus.Forwarded && hop.IsPending)
                    request.Forward(UserId.From(hopDto.ParticipantId), hopDto.ForwardNote, hopActionAt);
                else if (hopStatus == HopStatus.Declined && hop.IsPending)
                    request.Decline(UserId.From(hopDto.ParticipantId), hopActionAt);
                else if (hopStatus == HopStatus.Accepted && hop.IsPending)
                    request.Accept(UserId.From(hopDto.ParticipantId), hopActionAt);
            }
        }

        request.ClearDomainEvents(); // hydration must not re-fire events
        return request;
    }

    private sealed record HopDto(
        int Index,
        string ParticipantId,
        string Status,
        string? ForwardNote,
        string CreatedAt,
        string? ActionTakenAt);
}
