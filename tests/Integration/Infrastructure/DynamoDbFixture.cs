using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using JbNet.Infrastructure.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JbNet.Tests.Integration.Infrastructure;

/// <summary>
/// xUnit class fixture that creates a DynamoDB Local table before all tests
/// and drops it after all tests. Uses environment variable overrides for the
/// DynamoDB service URL and table name so CI can inject its own values.
/// </summary>
public sealed class DynamoDbFixture : IAsyncLifetime
{
    public IServiceProvider Services { get; private set; } = null!;
    public string TableName { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        TableName = Environment.GetEnvironmentVariable("DynamoDB__TableName") ?? "jbnet-table-test";
        var serviceUrl = Environment.GetEnvironmentVariable("DynamoDB__ServiceUrl") ?? "http://localhost:8000";

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DynamoDB:TableName"]  = TableName,
                ["DynamoDB:ServiceUrl"] = serviceUrl,
                ["S3:ResumeBucketName"] = "jbnet-resume-test",
                ["EventBridge:BusName"] = "jbnet-bus-test"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructureServices(configuration);

        Services = services.BuildServiceProvider();

        // Create the table in DynamoDB Local
        var dynamo = Services.GetRequiredService<IAmazonDynamoDB>();
        await CreateTableAsync(dynamo, TableName);
    }

    public async Task DisposeAsync()
    {
        var dynamo = Services.GetRequiredService<IAmazonDynamoDB>();
        try
        {
            await dynamo.DeleteTableAsync(TableName);
        }
        catch (ResourceNotFoundException)
        {
            // Table already gone — fine
        }
    }

    private static async Task CreateTableAsync(IAmazonDynamoDB dynamo, string tableName)
    {
        // Drop any stale table from a previous run
        try { await dynamo.DeleteTableAsync(tableName); } catch (ResourceNotFoundException) { }

        await dynamo.CreateTableAsync(new CreateTableRequest
        {
            TableName            = tableName,
            BillingMode          = BillingMode.PAY_PER_REQUEST,
            KeySchema            = new List<KeySchemaElement>
            {
                new() { AttributeName = "PK", KeyType = KeyType.HASH  },
                new() { AttributeName = "SK", KeyType = KeyType.RANGE }
            },
            AttributeDefinitions = new List<AttributeDefinition>
            {
                new() { AttributeName = "PK",     AttributeType = ScalarAttributeType.S },
                new() { AttributeName = "SK",     AttributeType = ScalarAttributeType.S },
                new() { AttributeName = "GSI1PK", AttributeType = ScalarAttributeType.S },
                new() { AttributeName = "GSI1SK", AttributeType = ScalarAttributeType.S }
            },
            GlobalSecondaryIndexes = new List<GlobalSecondaryIndex>
            {
                new()
                {
                    IndexName  = "GSI1-PendingByParticipant",
                    KeySchema  = new List<KeySchemaElement>
                    {
                        new() { AttributeName = "GSI1PK", KeyType = KeyType.HASH  },
                        new() { AttributeName = "GSI1SK", KeyType = KeyType.RANGE }
                    },
                    Projection = new Projection { ProjectionType = ProjectionType.ALL }
                }
            }
        });

        // Wait until table is ACTIVE
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            var desc = await dynamo.DescribeTableAsync(tableName);
            if (desc.Table.TableStatus == TableStatus.ACTIVE) return;
            await Task.Delay(200);
        }

        throw new TimeoutException($"DynamoDB table '{tableName}' did not become ACTIVE within 30 seconds.");
    }
}
