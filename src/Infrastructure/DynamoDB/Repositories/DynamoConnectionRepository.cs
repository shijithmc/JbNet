using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using JbNet.Domain.Aggregates.Users;
using JbNet.Domain.Enums;
using JbNet.Domain.Repositories;
using JbNet.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JbNet.Infrastructure.DynamoDB.Repositories;

public sealed class DynamoConnectionRepository(
    IAmazonDynamoDB dynamoDb,
    IOptions<DynamoDbOptions> options,
    ILogger<DynamoConnectionRepository> logger) : IConnectionRepository
{
    private readonly string _tableName = options.Value.TableName;

    public async Task<Connection?> GetAsync(UserId ownerId, UserId targetId, CancellationToken ct)
    {
        var response = await dynamoDb.GetItemAsync(new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                [DynamoTableKeys.PkAttr] = new(DynamoTableKeys.UserPk(ownerId.Value)),
                [DynamoTableKeys.SkAttr] = new(DynamoTableKeys.UserConnSk(targetId.Value))
            }
        }, ct);

        return response.IsItemSet ? MapFromItem(response.Item) : null;
    }

    public async Task SaveAsync(Connection connection, CancellationToken ct)
    {
        await dynamoDb.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = MapToItem(connection)
        }, ct);
    }

    public async Task SaveBothDirectionsAsync(Connection requesterRecord, Connection targetRecord, CancellationToken ct)
    {
        var transact = new TransactWriteItemsRequest
        {
            TransactItems =
            [
                new TransactWriteItem { Put = new Put { TableName = _tableName, Item = MapToItem(requesterRecord) } },
                new TransactWriteItem { Put = new Put { TableName = _tableName, Item = MapToItem(targetRecord) } }
            ]
        };

        try
        {
            await dynamoDb.TransactWriteItemsAsync(transact, ct);
        }
        catch (TransactionCanceledException ex)
        {
            logger.LogError(ex, "Transaction failed saving bidirectional connection {A} ↔ {B}",
                requesterRecord.OwnerId.Value, targetRecord.OwnerId.Value);
            throw;
        }
    }

    public async Task<IReadOnlyList<Connection>> GetAcceptedConnectionsAsync(UserId userId, CancellationToken ct)
    {
        var response = await dynamoDb.QueryAsync(new QueryRequest
        {
            TableName = _tableName,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :prefix)",
            FilterExpression = "ConnectionStatus = :status",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"]     = new(DynamoTableKeys.UserPk(userId.Value)),
                [":prefix"] = new("CONN#"),
                [":status"] = new(ConnectionStatus.Accepted.ToString())
            }
        }, ct);

        return response.Items.Select(MapFromItem).ToList().AsReadOnly();
    }

    public async Task DeleteBothDirectionsAsync(UserId userA, UserId userB, CancellationToken ct)
    {
        var transact = new TransactWriteItemsRequest
        {
            TransactItems =
            [
                new TransactWriteItem
                {
                    Delete = new Delete
                    {
                        TableName = _tableName,
                        Key = new Dictionary<string, AttributeValue>
                        {
                            [DynamoTableKeys.PkAttr] = new(DynamoTableKeys.UserPk(userA.Value)),
                            [DynamoTableKeys.SkAttr] = new(DynamoTableKeys.UserConnSk(userB.Value))
                        }
                    }
                },
                new TransactWriteItem
                {
                    Delete = new Delete
                    {
                        TableName = _tableName,
                        Key = new Dictionary<string, AttributeValue>
                        {
                            [DynamoTableKeys.PkAttr] = new(DynamoTableKeys.UserPk(userB.Value)),
                            [DynamoTableKeys.SkAttr] = new(DynamoTableKeys.UserConnSk(userA.Value))
                        }
                    }
                }
            ]
        };

        await dynamoDb.TransactWriteItemsAsync(transact, ct);
    }

    public async Task<bool> AreConnectedAsync(UserId userA, UserId userB, CancellationToken ct)
    {
        var connection = await GetAsync(userA, userB, ct);
        return connection?.IsAccepted ?? false;
    }

    private static Dictionary<string, AttributeValue> MapToItem(Connection conn) =>
        new()
        {
            [DynamoTableKeys.PkAttr]         = new(DynamoTableKeys.UserPk(conn.OwnerId.Value)),
            [DynamoTableKeys.SkAttr]         = new(DynamoTableKeys.UserConnSk(conn.TargetId.Value)),
            [DynamoTableKeys.EntityTypeAttr] = new("Connection"),
            ["ConnectionId"]                 = new(conn.Id.Value),
            ["OwnerId"]                      = new(conn.OwnerId.Value),
            ["TargetId"]                     = new(conn.TargetId.Value),
            ["ConnectionStatus"]             = new(conn.Status.ToString()),
            ["InviteNote"]                   = conn.InviteNote != null ? new(conn.InviteNote) : new AttributeValue { NULL = true },
            ["CreatedAt"]                    = new(conn.CreatedAt.ToString("O")),
            ["UpdatedAt"]                    = new(conn.UpdatedAt.ToString("O"))
        };

    private static Connection MapFromItem(Dictionary<string, AttributeValue> item)
    {
        var id = ConnectionId.From(item["ConnectionId"].S);
        var ownerId = UserId.From(item["OwnerId"].S);
        var targetId = UserId.From(item["TargetId"].S);
        var note = item.TryGetValue("InviteNote", out var n) && !n.NULL ? n.S : null;
        var createdAt = DateTimeOffset.Parse(item["CreatedAt"].S);

        var conn = Connection.CreatePending(id, ownerId, targetId, note, createdAt);

        var status = Enum.Parse<ConnectionStatus>(item["ConnectionStatus"].S);
        var updatedAt = DateTimeOffset.Parse(item["UpdatedAt"].S);

        if (status == ConnectionStatus.Accepted) conn.Accept(updatedAt);
        else if (status == ConnectionStatus.Declined) conn.Decline(updatedAt);
        else if (status == ConnectionStatus.Removed) conn.Remove(updatedAt);

        return conn;
    }
}
