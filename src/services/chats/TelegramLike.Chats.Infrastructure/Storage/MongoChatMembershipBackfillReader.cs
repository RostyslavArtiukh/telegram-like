using MongoDB.Driver;
using TelegramLike.Chats.Application.Backfill;
using TelegramLike.Chats.Domain.ValueObjects;

namespace TelegramLike.Chats.Infrastructure.Storage;

internal sealed class MongoChatMembershipBackfillReader(IMongoDatabase database) : IChatMembershipBackfillReader
{
    private readonly IMongoCollection<ChatMemberDocument> _chatMembersCollection =
        database.GetCollection<ChatMemberDocument>("chat_members");
    private readonly IMongoCollection<ChatDocument> _chatsCollection =
        database.GetCollection<ChatDocument>("chats");

    public async Task<IReadOnlyList<ChatMembershipSnapshot>> GetActiveMembershipsByChatAsync(
        CancellationToken cancellationToken = default)
    {
        var active = await _chatMembersCollection
            .Find(Builders<ChatMemberDocument>.Filter.Eq(d => d.Status, MemberStatus.Active))
            .ToListAsync(cancellationToken);

        var chatTypes = (await _chatsCollection
                .Find(Builders<ChatDocument>.Filter.Empty)
                .Project(c => new { c.Id, c.Type })
                .ToListAsync(cancellationToken))
            .ToDictionary(c => c.Id, c => c.Type.ToString());

        return active
            .GroupBy(m => m.ChatId)
            .Select(g => new ChatMembershipSnapshot(
                g.Key,
                chatTypes.GetValueOrDefault(g.Key, ChatType.Group.ToString()),
                g.Select(m => new ChatMembershipSnapshotMember(m.UserId, m.Role.ToString(), m.JoinedAt)).ToList()))
            .ToList();
    }
}
