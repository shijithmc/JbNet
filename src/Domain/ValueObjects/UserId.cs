namespace JbNet.Domain.ValueObjects;

/// <summary>Strongly-typed identifier for a User. Backed by a UUID string.</summary>
public sealed record UserId(string Value)
{
    public static UserId New() => new(Guid.NewGuid().ToString());
    public static UserId From(string value) => new(value);
    public override string ToString() => Value;
}
