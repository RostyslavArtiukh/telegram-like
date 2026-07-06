using MongoDB.Driver;
using TelegramLike.Identity.Domain.Aggregates;
using TelegramLike.Identity.Domain.Repositories;
using TelegramLike.Identity.Domain.ValueObjects;

namespace TelegramLike.Identity.Infrastructure.Persistence;

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

    public async Task AddAsync(User user, CancellationToken ct = default)
    {
        try
        {
            await _collection.InsertOneAsync(UserDocument.FromDomain(user), cancellationToken: ct);
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            // Race backstop for the check-then-act in RegisterUserCommandHandler: the
            // unique email/username index rejected a concurrent duplicate. Map to the
            // same 400 {error} the pre-check would have produced.
            throw new InvalidOperationException("Email or username is already taken.");
        }
    }

    public async Task UpdateAsync(User user, CancellationToken ct = default) =>
        await _collection.ReplaceOneAsync(u => u.Id == user.Id, UserDocument.FromDomain(user),
            new ReplaceOptions { IsUpsert = false }, ct);
}
