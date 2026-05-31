using MongoDB.Driver;
using TelegramLike.Presence.Application.Abstractions;

namespace TelegramLike.Presence.Infrastructure.Persistence;

internal sealed class MongoChatMembershipReadModel(IMongoDatabase database) : IChatMembershipReadModel
{
    private readonly IMongoCollection<ChatMembershipDocument> _memberships =
        database.GetCollection<ChatMembershipDocument>("chat_memberships");

    public async Task<bool> IsActiveMemberAsync(Guid chatId, Guid userId, CancellationToken ct = default)
    {
        var id = ChatMembershipDocument.MakeId(chatId, userId);
        var found = await _memberships
            .Find(d => d.Id == id)
            .Limit(1)
            .AnyAsync(ct);
        return found;
    }

    public Task UpsertActiveAsync(Guid chatId, Guid userId, CancellationToken ct = default)
    {
        var doc = new ChatMembershipDocument
        {
            Id = ChatMembershipDocument.MakeId(chatId, userId),
            ChatId = chatId,
            UserId = userId
        };
        return _memberships.ReplaceOneAsync(
            Builders<ChatMembershipDocument>.Filter.Eq(d => d.Id, doc.Id),
            doc,
            new ReplaceOptions { IsUpsert = true },
            ct);
    }

    public Task RemoveAsync(Guid chatId, Guid userId, CancellationToken ct = default)
        => _memberships.DeleteOneAsync(
            Builders<ChatMembershipDocument>.Filter.Eq(d => d.Id, ChatMembershipDocument.MakeId(chatId, userId)),
            ct);
}
