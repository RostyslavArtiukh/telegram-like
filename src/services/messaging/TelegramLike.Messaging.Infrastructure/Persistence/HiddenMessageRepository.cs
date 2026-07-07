using MongoDB.Driver;
using TelegramLike.Messaging.Application.Common.Interfaces;

namespace TelegramLike.Messaging.Infrastructure.Persistence;

internal sealed class HiddenMessageRepository(IMongoDatabase database) : IHiddenMessageRepository
{
    private readonly IMongoCollection<HiddenMessageDocument> _hidden =
        database.GetCollection<HiddenMessageDocument>("hidden_messages");

    public async Task HideAsync(Guid messageId, Guid userId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<HiddenMessageDocument>.Filter.And(
            Builders<HiddenMessageDocument>.Filter.Eq(h => h.MessageId, messageId),
            Builders<HiddenMessageDocument>.Filter.Eq(h => h.UserId, userId));

        var update = Builders<HiddenMessageDocument>.Update
            .SetOnInsert(h => h.Id, Guid.NewGuid())
            .SetOnInsert(h => h.MessageId, messageId)
            .SetOnInsert(h => h.UserId, userId);

        await _hidden.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true }, cancellationToken);
    }

    public Task<bool> IsHiddenAsync(Guid messageId, Guid userId, CancellationToken cancellationToken = default)
        => _hidden.Find(h => h.MessageId == messageId && h.UserId == userId).AnyAsync(cancellationToken);
}
