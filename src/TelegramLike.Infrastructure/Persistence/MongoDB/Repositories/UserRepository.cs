using MongoDB.Driver;
using TelegramLike.Domain.Identity.Aggregates;
using TelegramLike.Domain.Identity.Repositories;
using TelegramLike.Domain.Identity.ValueObjects;

namespace TelegramLike.Infrastructure.Persistence.MongoDB.Repositories;

internal sealed class UserRepository(IMongoDatabase database) : IUserRepository
{
    private readonly IMongoCollection<UserDocument> _collection =
        database.GetCollection<UserDocument>("users");

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var doc = await _collection
            .Find(u => u.Id == id)
            .FirstOrDefaultAsync(ct);
        return doc?.ToDomain();
    }

    public async Task<User?> GetByEmailAsync(Email email, CancellationToken ct = default)
    {
        var doc = await _collection
            .Find(u => u.Email == email.Value)
            .FirstOrDefaultAsync(ct);
        return doc?.ToDomain();
    }

    public async Task<User?> GetByUsernameAsync(Username username, CancellationToken ct = default)
    {
        var doc = await _collection
            .Find(u => u.Username == username.Value)
            .FirstOrDefaultAsync(ct);
        return doc?.ToDomain();
    }

    public async Task<bool> ExistsByEmailAsync(Email email, CancellationToken ct = default) =>
        await _collection.Find(u => u.Email == email.Value).AnyAsync(ct);

    public async Task<bool> ExistsByUsernameAsync(Username username, CancellationToken ct = default) =>
        await _collection.Find(u => u.Username == username.Value).AnyAsync(ct);

    public async Task<IReadOnlyList<User>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0) return [];

        var docs = await _collection
            .Find(Builders<UserDocument>.Filter.In(u => u.Id, ids))
            .ToListAsync(ct);

        return docs.Select(d => d.ToDomain()).ToList();
    }

    public async Task AddAsync(User user, CancellationToken ct = default) =>
        await _collection.InsertOneAsync(UserDocument.FromDomain(user), cancellationToken: ct);

    public async Task UpdateAsync(User user, CancellationToken ct = default) =>
        await _collection.ReplaceOneAsync(u => u.Id == user.Id, UserDocument.FromDomain(user),
            new ReplaceOptions { IsUpsert = false }, ct);
}
