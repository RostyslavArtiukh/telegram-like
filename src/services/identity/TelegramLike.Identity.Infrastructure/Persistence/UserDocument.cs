using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using TelegramLike.Identity.Domain.Aggregates;

namespace TelegramLike.Identity.Infrastructure.Persistence;

internal sealed class UserDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }

    public string Email { get; set; } = null!;
    public string Username { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public string? AvatarUrl { get; set; }
    public AccountStatus Status { get; set; }
    public bool IsPremium { get; set; }
    public DateTime? PremiumExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    [BsonRepresentation(BsonType.String)]
    public List<Guid> BlockedUserIds { get; set; } = [];

    public static UserDocument FromDomain(User user) => new()
    {
        Id = user.Id,
        Email = user.Email.Value,
        Username = user.Username.Value,
        DisplayName = user.DisplayName.Value,
        PasswordHash = user.Password.Hash,
        AvatarUrl = user.AvatarUrl,
        Status = user.Status,
        IsPremium = user.IsPremium,
        PremiumExpiresAt = user.PremiumExpiresAt,
        CreatedAt = user.CreatedAt,
        UpdatedAt = user.UpdatedAt,
        BlockedUserIds = [..user.BlockedUserIds]
    };

    public User ToDomain() => User.Reconstitute(
        Id, Email, Username, DisplayName, PasswordHash,
        AvatarUrl, Status, IsPremium, PremiumExpiresAt,
        BlockedUserIds, CreatedAt, UpdatedAt);
}
