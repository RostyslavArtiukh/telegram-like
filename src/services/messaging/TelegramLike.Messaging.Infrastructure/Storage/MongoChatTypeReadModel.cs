using MongoDB.Driver;
using TelegramLike.Messaging.Application.Storage;

namespace TelegramLike.Messaging.Infrastructure.Storage;

internal sealed class MongoChatTypeReadModel(IMongoDatabase database) : IChatTypeReadModel
{
    private const string BroadcastType = "Broadcast";

    private readonly IMongoCollection<ChatTypeDocument> _chatTypesCollection =
        database.GetCollection<ChatTypeDocument>("chat_types");

    public async Task<bool?> IsBroadcastAsync(Guid chatId, CancellationToken cancellationToken = default)
    {
        var doc = await _chatTypesCollection
            .Find(Builders<ChatTypeDocument>.Filter.Eq(d => d.Id, chatId))
            .Limit(1)
            .FirstOrDefaultAsync(cancellationToken);

        return doc is null ? null : string.Equals(doc.Type, BroadcastType, StringComparison.OrdinalIgnoreCase);
    }

    // Chat type is immutable, so $setOnInsert makes redelivery a no-op without a timestamp guard.
    public Task UpsertAsync(Guid chatId, string chatType, CancellationToken cancellationToken = default)
    {
        var update = Builders<ChatTypeDocument>.Update
            .SetOnInsert(d => d.Id, chatId)
            .SetOnInsert(d => d.Type, chatType);

        return _chatTypesCollection.UpdateOneAsync(
            Builders<ChatTypeDocument>.Filter.Eq(d => d.Id, chatId),
            update,
            new UpdateOptions { IsUpsert = true },
            cancellationToken);
    }
}
