using TelegramLike.Shared.Domain;
using MongoDB.Driver;
using TelegramLike.Identity.Domain;
using TelegramLike.Identity.Domain.Aggregates;
using TelegramLike.Identity.Domain.Repositories;
using TelegramLike.Identity.Domain.ValueObjects;

namespace TelegramLike.Identity.Infrastructure.Storage;

internal sealed class UserRepository(IMongoDatabase database) : IUserRepository
{
    private readonly IMongoCollection<UserDocument> _usersCollection =
        database.GetCollection<UserDocument>("users");

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var doc = await _usersCollection
            .Find(u => u.Id == id)
            .FirstOrDefaultAsync(cancellationToken);
        return doc?.ToDomain();
    }

    public async Task<User?> GetByEmailAsync(Email email, CancellationToken cancellationToken = default)
    {
        var doc = await _usersCollection
            .Find(u => u.Email == email.Value)
            .FirstOrDefaultAsync(cancellationToken);
        return doc?.ToDomain();
    }

    public async Task<User?> GetByUsernameAsync(Username username, CancellationToken cancellationToken = default)
    {
        var doc = await _usersCollection
            .Find(u => u.Username == username.Value)
            .FirstOrDefaultAsync(cancellationToken);
        return doc?.ToDomain();
    }

    public async Task<bool> ExistsByEmailAsync(Email email, CancellationToken cancellationToken = default) =>
        await _usersCollection.Find(u => u.Email == email.Value).AnyAsync(cancellationToken);

    public async Task<bool> ExistsByUsernameAsync(Username username, CancellationToken cancellationToken = default) =>
        await _usersCollection.Find(u => u.Username == username.Value).AnyAsync(cancellationToken);

    public async Task<IReadOnlyList<User>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0) return [];

        var docs = await _usersCollection
            .Find(Builders<UserDocument>.Filter.In(u => u.Id, ids))
            .ToListAsync(cancellationToken);

        return docs.Select(d => d.ToDomain()).ToList();
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        try
        {
            await _usersCollection.InsertOneAsync(UserDocument.FromDomain(user), cancellationToken: cancellationToken);
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            // Race backstop for the check-then-act in RegisterUserCommandHandler: the
            // unique email/username index rejected a concurrent duplicate. A DomainException
            // (business rule) keeps the same 400 {error} the pre-check would have produced.
            throw new DomainException("Email or username is already taken.");
        }
    }

    public async Task UpdateAsync(User user, CancellationToken cancellationToken = default) =>
        await _usersCollection.ReplaceOneAsync(u => u.Id == user.Id, UserDocument.FromDomain(user),
            new ReplaceOptions { IsUpsert = false }, cancellationToken);
}
