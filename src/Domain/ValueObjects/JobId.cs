namespace JbNet.Domain.ValueObjects;

/// <summary>Strongly-typed identifier for a JobPosting.</summary>
public sealed record JobId(string Value)
{
    public static JobId New() => new(Guid.NewGuid().ToString());
    public static JobId From(string value) => new(value);
    public override string ToString() => Value;
}
