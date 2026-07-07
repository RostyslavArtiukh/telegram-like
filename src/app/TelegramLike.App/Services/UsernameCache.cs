using System.Collections.Concurrent;
using TelegramLike.Client.Identity;

namespace TelegramLike.App.Services;

/// <summary>
/// Resolves userIds to usernames via Identity, cached for the app's lifetime.
/// Unknown ids render as a short id until <see cref="EnsureAsync"/> fills them in.
/// </summary>
public sealed class UsernameCache(IIdentityUsersApi users)
{
    private readonly ConcurrentDictionary<Guid, string> _names = new();

    public string Get(Guid userId)
        => _names.TryGetValue(userId, out var name) ? name : userId.ToString()[..8];

    public async Task EnsureAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken = default)
    {
        var missing = userIds.Distinct().Where(id => !_names.ContainsKey(id)).ToList();
        if (missing.Count == 0) return;

        var map = await users.GetUsernamesByIdsAsync(missing, cancellationToken);
        foreach (var (id, name) in map)
            _names[id] = name;
    }
}
