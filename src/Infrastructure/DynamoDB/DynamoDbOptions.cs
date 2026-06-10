namespace JbNet.Infrastructure.DynamoDB;

public sealed class DynamoDbOptions
{
    public const string SectionName = "DynamoDB";

    /// <summary>Name of the single DynamoDB table. Injected from environment / Secrets Manager.</summary>
    public string TableName { get; set; } = "jbnet-table";

    /// <summary>Optional endpoint for DynamoDB Local during local development.</summary>
    public string? ServiceUrl { get; set; }
}
