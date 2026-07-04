using MongoDB.Driver;
using TelegramLike.Chats.Domain.Aggregates;
using TelegramLike.Chats.Domain.Repositories;
using TelegramLike.Chats.Domain.ValueObjects;
using TelegramLike.Chats.Infrastructure.Outbox;

namespace TelegramLike.Chats.Infrastructure.Persistence;

internal sealed class ChatRepository(
    IMongoClient mongoClient,
    IMongoDatabase database,
    IDomainEventDispatcher dispatcher) : IChatRepository
{
    private readonly IMongoCollection<ChatDocument> _chats = database.GetCollection<ChatDocument>("chats");
    private readonly IMongoCollection<ChatMemberDocument> _members = database.GetCollection<ChatMemberDocument>("chat_members");

    public async Task<Chat?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var chatDoc = await _chats.Find(c => c.Id == id).FirstOrDefaultAsync(ct);
        if (chatDoc is null) return null;

        var memberDocs = await _members.Find(m => m.ChatId == id).ToListAsync(ct);
        return Reconstitute(chatDoc, memberDocs);
    }

    public async Task<DirectChat?> FindDirectBetweenAsync(Guid userA, Guid userB, CancellationToken ct = default)
    {
        var memberDocs = await _members
            .Find(m => m.UserId == userA || m.UserId == userB)
            .ToListAsync(ct);

        var candidateChatIds = memberDocs
            .GroupBy(m => m.ChatId)
            .Where(g => g.Any(m => m.UserId == userA) && g.Any(m => m.UserId == userB))
            .Select(g => g.Key)
            .ToList();

        if (candidateChatIds.Count == 0) return null;

        var chatDoc = await _chats
            .Find(c => candidateChatIds.Contains(c.Id) && c.Type == ChatType.Direct && c.DeletedAt == null)
            .FirstOrDefaultAsync(ct);

        if (chatDoc is null) return null;

        var allMembers = memberDocs.Where(m => m.ChatId == chatDoc.Id).ToList();
        return (DirectChat?)Reconstitute(chatDoc, allMembers);
    }

    public async Task AddAsync(Chat chat, CancellationToken ct = default)
    {
        try
        {
            using var session = await mongoClient.StartSessionAsync(cancellationToken: ct);
            await session.WithTransactionAsync(async (s, token) =>
            {
                await _chats.InsertOneAsync(s, ToChatDocument(chat), cancellationToken: token);

                var memberDocs = chat.Members.Select(m => ChatMemberDocument.FromDomain(m, chat.Id)).ToList();
                if (memberDocs.Count > 0)
                    await _members.InsertManyAsync(s, memberDocs, cancellationToken: token);

                await dispatcher.DispatchAsync(chat.DomainEvents, s, token);
                return true;
            }, cancellationToken: ct);
        }
        catch (Exception ex) when (IsDuplicateKey(ex))
        {
            // Idempotent retry: a chat already exists with this id (the client reused it
            // on a retry). The transaction aborted, so nothing was re-inserted and no
            // ChatCreated/MemberJoined events were re-queued to the outbox.
        }

        chat.ClearDomainEvents();
    }

    // A duplicate _id surfaces differently depending on where Mongo detects it
    // (write vs. command vs. bulk), so check all three for error code 11000.
    private static bool IsDuplicateKey(Exception ex) => ex switch
    {
        MongoWriteException we => we.WriteError?.Category == ServerErrorCategory.DuplicateKey,
        MongoCommandException ce => ce.Code == 11000,
        MongoBulkWriteException be => be.WriteErrors.Any(e => e.Category == ServerErrorCategory.DuplicateKey),
        _ => false
    };

    public async Task UpdateAsync(Chat chat, CancellationToken ct = default)
    {
        using var session = await mongoClient.StartSessionAsync(cancellationToken: ct);
        await session.WithTransactionAsync(async (s, token) =>
        {
            await _chats.ReplaceOneAsync(
                s,
                Builders<ChatDocument>.Filter.Eq(c => c.Id, chat.Id),
                ToChatDocument(chat),
                new ReplaceOptions { IsUpsert = false },
                token);

            var memberOps = chat.Members.Select(m => (WriteModel<ChatMemberDocument>)
                new ReplaceOneModel<ChatMemberDocument>(
                    Builders<ChatMemberDocument>.Filter.Eq(x => x.Id, m.Id),
                    ChatMemberDocument.FromDomain(m, chat.Id))
                {
                    IsUpsert = true
                }).ToList();

            if (memberOps.Count > 0)
                await _members.BulkWriteAsync(s, memberOps, cancellationToken: token);

            await dispatcher.DispatchAsync(chat.DomainEvents, s, token);
            return true;
        }, cancellationToken: ct);

        chat.ClearDomainEvents();
    }

    private static ChatDocument ToChatDocument(Chat chat) => new()
    {
        Id = chat.Id,
        Type = chat.Type,
        Name = chat.Name?.Value,
        CreatedBy = chat.CreatedBy,
        CreatedAt = chat.CreatedAt,
        DeletedAt = chat.DeletedAt
    };

    private static Chat Reconstitute(ChatDocument chatDoc, List<ChatMemberDocument> memberDocs)
    {
        var members = memberDocs.Select(m => m.ToDomain()).ToList();

        return chatDoc.Type switch
        {
            ChatType.Direct => DirectChat.Reconstitute(
                chatDoc.Id, chatDoc.CreatedBy, chatDoc.CreatedAt, chatDoc.DeletedAt, members),
            ChatType.Group => GroupChat.Reconstitute(
                chatDoc.Id,
                ChatName.Create(chatDoc.Name ?? throw new InvalidOperationException("GroupChat must have a name.")),
                chatDoc.CreatedBy, chatDoc.CreatedAt, chatDoc.DeletedAt, members),
            ChatType.Broadcast => BroadcastChannel.Reconstitute(
                chatDoc.Id,
                ChatName.Create(chatDoc.Name ?? throw new InvalidOperationException("BroadcastChannel must have a name.")),
                chatDoc.CreatedBy, chatDoc.CreatedAt, chatDoc.DeletedAt, members),
            _ => throw new InvalidOperationException($"Unknown chat type: {chatDoc.Type}")
        };
    }
}
