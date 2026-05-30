using TelegramLike.Domain.Common;
using TelegramLike.Domain.Identity.Events;
using TelegramLike.Domain.Identity.ValueObjects;

namespace TelegramLike.Domain.Identity.Aggregates;

public enum AccountStatus { Active, Banned, Deleted }

public sealed class User : AggregateRoot
{
    private readonly List<Guid> _blockedUserIds = [];

    public Email Email { get; private set; } = null!;
    public Username Username { get; private set; } = null!;
    public DisplayName DisplayName { get; private set; } = null!;
    public HashedPassword Password { get; private set; } = null!;
    public string? AvatarUrl { get; private set; }
    public AccountStatus Status { get; private set; }
    public bool IsPremium { get; private set; }
    public DateTime? PremiumExpiresAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public IReadOnlyList<Guid> BlockedUserIds => _blockedUserIds.AsReadOnly();

    private User() { }

    private User(Guid id, Email email, Username username, DisplayName displayName, HashedPassword password,
        AccountStatus status, bool isPremium, DateTime? premiumExpiresAt, string? avatarUrl,
        List<Guid> blockedUserIds, DateTime createdAt, DateTime updatedAt)
        : base(id)
    {
        Email = email;
        Username = username;
        DisplayName = displayName;
        Password = password;
        Status = status;
        IsPremium = isPremium;
        PremiumExpiresAt = premiumExpiresAt;
        AvatarUrl = avatarUrl;
        _blockedUserIds = blockedUserIds;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public static User Register(string email, string username, string displayName, string passwordHash)
    {
        var now = DateTime.UtcNow;
        var user = new User(
            Guid.NewGuid(),
            Email.Create(email),
            Username.Create(username),
            DisplayName.Create(displayName),
            HashedPassword.FromHash(passwordHash),
            AccountStatus.Active,
            isPremium: false,
            premiumExpiresAt: null,
            avatarUrl: null,
            blockedUserIds: [],
            createdAt: now,
            updatedAt: now);

        user.RaiseDomainEvent(new UserRegisteredEvent(user.Id, email, username));
        return user;
    }

    // Factory for reconstituting from persistence (no domain events raised)
    public static User Reconstitute(
        Guid id, string email, string username, string displayName, string passwordHash,
        string? avatarUrl, AccountStatus status, bool isPremium, DateTime? premiumExpiresAt,
        List<Guid> blockedUserIds, DateTime createdAt, DateTime updatedAt)
    {
        return new User(id,
            Email.Create(email),
            Username.Create(username),
            DisplayName.Create(displayName),
            HashedPassword.FromHash(passwordHash),
            status, isPremium, premiumExpiresAt, avatarUrl,
            blockedUserIds, createdAt, updatedAt);
    }

    public void UpdateDisplayName(DisplayName newName)
    {
        DisplayName = newName;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetAvatar(string? avatarUrl)
    {
        AvatarUrl = avatarUrl;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Block(Guid targetUserId)
    {
        if (targetUserId == Id)
            throw new InvalidOperationException("Cannot block yourself.");

        if (_blockedUserIds.Contains(targetUserId))
            return;

        _blockedUserIds.Add(targetUserId);
        UpdatedAt = DateTime.UtcNow;
        RaiseDomainEvent(new UserBlockedEvent(Id, targetUserId));
    }

    public void Unblock(Guid targetUserId)
    {
        _blockedUserIds.Remove(targetUserId);
        UpdatedAt = DateTime.UtcNow;
    }

    public bool HasBlocked(Guid userId) => _blockedUserIds.Contains(userId);

    public void ActivatePremium(DateTime expiresAt)
    {
        IsPremium = true;
        PremiumExpiresAt = expiresAt;
        UpdatedAt = DateTime.UtcNow;
    }

    public void CheckPremiumExpiry()
    {
        if (IsPremium && PremiumExpiresAt.HasValue && PremiumExpiresAt.Value < DateTime.UtcNow)
        {
            IsPremium = false;
            PremiumExpiresAt = null;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
