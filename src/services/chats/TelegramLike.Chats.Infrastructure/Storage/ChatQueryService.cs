using MongoDB.Driver;
using TelegramLike.Chats.Application.Queries;
using TelegramLike.Chats.Domain.ValueObjects;

namespace TelegramLike.Chats.Infrastructure.Storage;

internal sealed class ChatQueryService(IMongoDatabase database) : IChatQueryService
{
    private readonly IMongoCollection<ChatDocument> _chatsCollection = database.GetCollection<ChatDocument>("chats");
    private readonly IMongoCollection<ChatMemberDocument> _chatMembersCollection = database.GetCollection<ChatMemberDocument>("chat_members");

    public async Task<IReadOnlyList<ChatSummaryDto>> GetMyChatsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var myMemberships = await _chatMembersCollection
            .Find(m => m.UserId == userId && m.Status == MemberStatus.Active)
            .ToListAsync(cancellationToken);

        if (myMemberships.Count == 0) return [];

        var chatIds = myMemberships.Select(m => m.ChatId).ToHashSet();
        var chats = await _chatsCollection
            .Find(c => chatIds.Contains(c.Id) && c.DeletedAt == null)
            .ToListAsync(cancellationToken);

        var activeCounts = await _chatMembersCollection
            .Aggregate()
            .Match(m => chatIds.Contains(m.ChatId) && m.Status == MemberStatus.Active)
            .Group(m => m.ChatId, g => new { ChatId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var countMap = activeCounts.ToDictionary(x => x.ChatId, x => x.Count);

        return chats.Select(c =>
        {
            var myRole = myMemberships.First(m => m.ChatId == c.Id).Role;
            var count = countMap.TryGetValue(c.Id, out var n) ? n : 0;
            return new ChatSummaryDto(c.Id, c.Type, c.Name, myRole, count);
        }).ToList();
    }

    public async Task<ChatDetailsDto?> GetChatByIdAsync(Guid chatId, CancellationToken cancellationToken = default)
    {
        var chat = await _chatsCollection.Find(c => c.Id == chatId).FirstOrDefaultAsync(cancellationToken);
        if (chat is null) return null;

        var members = await _chatMembersCollection.Find(m => m.ChatId == chatId).ToListAsync(cancellationToken);

        return new ChatDetailsDto(
            chat.Id,
            chat.Type,
            chat.Name,
            chat.CreatedBy,
            chat.CreatedAt,
            chat.DeletedAt.HasValue,
            members.Select(MapMember).ToList());
    }

    public async Task<IReadOnlyList<ChatMemberDto>> GetChatMembersAsync(Guid chatId, CancellationToken cancellationToken = default)
    {
        var members = await _chatMembersCollection.Find(m => m.ChatId == chatId).ToListAsync(cancellationToken);
        return members.Select(MapMember).ToList();
    }

    public Task<bool> IsActiveMemberAsync(Guid chatId, Guid userId, CancellationToken cancellationToken = default)
        => _chatMembersCollection
            .Find(m => m.ChatId == chatId && m.UserId == userId && m.Status == MemberStatus.Active)
            .Limit(1)
            .AnyAsync(cancellationToken);

    private static ChatMemberDto MapMember(ChatMemberDocument m)
        => new(m.UserId, m.Role, m.Status, m.JoinedAt, m.LeftAt);
}
