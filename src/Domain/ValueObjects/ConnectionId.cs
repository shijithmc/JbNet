namespace JbNet.Domain.ValueObjects;

/// <summary>Strongly-typed identifier for a Connection record.</summary>
public sealed record ConnectionId(string Value)
{
    public static ConnectionId New() => new(Guid.NewGuid().ToString());
    public static ConnectionId From(string value) => new(value);
    public override string ToString() => Value;
}
