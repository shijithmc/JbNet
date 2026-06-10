using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using JbNet.Domain.Aggregates.Notifications;
using JbNet.Domain.Repositories;
using Microsoft.Extensions.Options;

namespace JbNet.Infrastructure.DynamoDB.Repositories;

/// <summary>
/// DynamoDB implementation of <see cref="IDeviceTokenRepository"/>.
/// Storage pattern:
///   PK = USER#{userId}
///   SK = DEVICE#{tokenId}
///   EntityType = "DeviceToken"
///   Data = JSON-serialised token fields
/// </summary>
internal sealed class DynamoDeviceTokenRepository : IDeviceTokenRepository
{
    private readonly IAmazonDynamoDB _dynamo;
    private readonly string _tableName;

    public DynamoDeviceTokenRepository(IAmazonDynamoDB dynamo, IOptions<DynamoDbOptions> options)
    {
        _dynamo    = dynamo;
        _tableName = options.Value.TableName;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetEndpointArnsAsync(string userId, CancellationToken ct = default)
    {
        var request = new QueryRequest
        {
            TableName                 = _tableName,
            KeyConditionExpression    = "#pk = :pk AND begins_with(#sk, :skPrefix)",
            ExpressionAttributeNames  = new Dictionary<string, string>
            {
                ["#pk"] = DynamoTableKeys.PkAttr,
                ["#sk"] = DynamoTableKeys.SkAttr
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"]       = new AttributeValue { S = DynamoTableKeys.UserPk(userId) },
                [":skPrefix"] = new AttributeValue { S = "DEVICE#" }
            },
            ProjectionExpression = "SnsEndpointArn"
        };

        var response = await _dynamo.QueryAsync(request, ct);

        return response.Items
            .Where(i => i.TryGetValue("SnsEndpointArn", out var v) && !string.IsNullOrEmpty(v.S))
            .Select(i => i["SnsEndpointArn"].S)
            .ToList()
            .AsReadOnly();
    }

    /// <inheritdoc />
    public async Task SaveAsync(DeviceToken token, CancellationToken ct = default)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            [DynamoTableKeys.PkAttr]         = new AttributeValue { S = DynamoTableKeys.UserPk(token.UserId) },
            [DynamoTableKeys.SkAttr]         = new AttributeValue { S = DynamoTableKeys.DeviceTokenSk(token.TokenId) },
            [DynamoTableKeys.EntityTypeAttr] = new AttributeValue { S = "DeviceToken" },
            ["TokenId"]                       = new AttributeValue { S = token.TokenId },
            ["UserId"]                        = new AttributeValue { S = token.UserId },
            ["RawToken"]                      = new AttributeValue { S = token.RawToken },
            ["SnsEndpointArn"]                = new AttributeValue { S = token.SnsEndpointArn },
            ["Platform"]                      = new AttributeValue { S = token.Platform },
            ["RegisteredAt"]                  = new AttributeValue { S = token.RegisteredAt.ToString("O") }
        };

        await _dynamo.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item      = item
        }, ct);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string userId, string tokenId, CancellationToken ct = default)
    {
        await _dynamo.DeleteItemAsync(new DeleteItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                [DynamoTableKeys.PkAttr] = new AttributeValue { S = DynamoTableKeys.UserPk(userId) },
                [DynamoTableKeys.SkAttr] = new AttributeValue { S = DynamoTableKeys.DeviceTokenSk(tokenId) }
            }
        }, ct);
    }
}
