using Amazon.DynamoDBv2.Model;
using JbNet.Domain.Aggregates.Users;
using JbNet.Domain.Enums;
using JbNet.Domain.ValueObjects;

namespace JbNet.Infrastructure.DynamoDB.Mappers;

/// <summary>Converts between User domain aggregate and DynamoDB AttributeValue item format.</summary>
internal static class UserMapper
{
    public static Dictionary<string, AttributeValue> ToItem(User user)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            [DynamoTableKeys.PkAttr]         = new(DynamoTableKeys.UserPk(user.Id.Value)),
            [DynamoTableKeys.SkAttr]         = new(DynamoTableKeys.UserProfileSk()),
            [DynamoTableKeys.EntityTypeAttr] = new("User"),
            ["UserId"]             = new(user.Id.Value),
            ["FullName"]           = new(user.FullName),
            ["FullNameLower"]      = new(user.FullName.ToLowerInvariant()),
            ["Email"]              = new(user.Email),
            ["Headline"]           = new(user.Headline),
            ["ActiveReferralCount"] = new AttributeValue { N = user.ActiveReferralCount.ToString() },
            ["ConnectionCount"]    = new AttributeValue { N = user.ConnectionCount.ToString() },
            ["Role"]               = new(user.Role.ToString()),
            ["IsActive"]           = new AttributeValue { BOOL = user.IsActive },
            ["CreatedAt"]          = new(user.CreatedAt.ToString("O")),
            ["UpdatedAt"]          = new(user.UpdatedAt.ToString("O")),
        };

        if (user.EmployerName != null)
        {
            item["EmployerName"]      = new(user.EmployerName);
            item["EmployerNameLower"] = new(user.EmployerName.ToLowerInvariant());
        }
        if (user.City != null) item["City"] = new(user.City);
        if (user.ProfilePhotoUrl != null) item["ProfilePhotoUrl"] = new(user.ProfilePhotoUrl);
        if (user.ResumeS3Key != null)
        {
            item["ResumeS3Key"]     = new(user.ResumeS3Key);
            item["ResumeFileName"]  = new(user.ResumeFileName!);
            item["ResumeSizeBytes"] = new AttributeValue { N = user.ResumeSizeBytes!.Value.ToString() };
        }

        return item;
    }

    public static User FromItem(Dictionary<string, AttributeValue> item)
    {
        var id = UserId.From(item["UserId"].S);
        var createdAt = DateTimeOffset.Parse(item["CreatedAt"].S);

        var user = User.Create(id, item["FullName"].S, item["Email"].S, createdAt);

        user.UpdateProfile(
            item["FullName"].S,
            item.TryGetValue("Headline", out var h) ? h.S : string.Empty,
            item.TryGetValue("EmployerName", out var emp) ? emp.S : null,
            item.TryGetValue("City", out var city) ? city.S : null,
            DateTimeOffset.Parse(item["UpdatedAt"].S));

        if (item.TryGetValue("ResumeS3Key", out var key))
            user.SetResume(
                key.S,
                item["ResumeFileName"].S,
                long.Parse(item["ResumeSizeBytes"].N),
                DateTimeOffset.Parse(item["UpdatedAt"].S));

        if (item.TryGetValue("ProfilePhotoUrl", out var photo))
            user.SetProfilePhoto(photo.S, DateTimeOffset.Parse(item["UpdatedAt"].S));

        // Restore mutable counters via reflection-free approach: call methods to sync state
        var activeCount = int.Parse(item["ActiveReferralCount"].N);
        for (int i = 0; i < activeCount; i++) user.IncrementActiveReferralCount();

        return user;
    }
}
