namespace JbNet.Domain.ValueObjects;

/// <summary>Strongly-typed identifier for a ReferralRequest.</summary>
public sealed record RequestId(string Value)
{
    public static RequestId New() => new(Guid.NewGuid().ToString());
    public static RequestId From(string value) => new(value);
    public override string ToString() => Value;
}
