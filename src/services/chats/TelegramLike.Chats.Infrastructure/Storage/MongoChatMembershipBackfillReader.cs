using MongoDB.Driver;
using TelegramLike.Chats.Application.Backfill;
using TelegramLike.Chats.Domain.ValueObjects;

namespace TelegramLike.Chats.Infrastructure.Storage;

internal sealed class MongoChatMembershipBackfillReader(IMongoDatabase database) : IChatMembershipBackfillReader
{
    private readonly IMongoCollection<ChatMemberDocument> _chatMembersCollection =
        database.GetCollection<ChatMemberDocument>("chat_members");

    public async Task<IReadOnlyList<ChatMembershipSnapshot>> GetActiveMembershipsByChatAsync(
        CancellationToken cancellationToken = default)
    {
        var active = await _chatMembersCollection
            .Find(Builders<ChatMemberDocument>.Filter.Eq(d => d.Status, MemberStatus.Active))
            .ToListAsync(cancellationToken);

        return active
            .GroupBy(m => m.ChatId)
            .Select(g => new ChatMembershipSnapshot(
                g.Key,
                g.Select(m => new ChatMembershipSnapshotMember(m.UserId, m.Role.ToString(), m.JoinedAt)).ToList()))
            .ToList();
    }
}
