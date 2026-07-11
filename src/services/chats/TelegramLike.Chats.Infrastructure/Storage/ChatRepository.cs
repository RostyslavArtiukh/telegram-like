using MongoDB.Driver;
using TelegramLike.Chats.Domain.Aggregates;
using TelegramLike.Chats.Domain.Repositories;
using TelegramLike.Chats.Domain.ValueObjects;
using TelegramLike.Infrastructure.ServiceDefaults.OutgoingEvents;

namespace TelegramLike.Chats.Infrastructure.Storage;

internal sealed class ChatRepository(
    IMongoClient mongoClient,
    IMongoDatabase database,
    IOutgoingEventsWriter dispatcher) : IChatRepository
{
    private readonly IMongoCollection<ChatDocument> _chatsCollection = database.GetCollection<ChatDocument>("chats");
    private readonly IMongoCollection<ChatMemberDocument> _chatMembersCollection = database.GetCollection<ChatMemberDocument>("chat_members");

    public async Task<Chat?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var chatDoc = await _chatsCollection.Find(c => c.Id == id).FirstOrDefaultAsync(cancellationToken);
        if (chatDoc is null) return null;

        var memberDocs = await _chatMembersCollection.Find(m => m.ChatId == id).ToListAsync(cancellationToken);
        return FromStorage(chatDoc, memberDocs);
    }

    public async Task<DirectChat?> FindDirectBetweenAsync(Guid userA, Guid userB, CancellationToken cancellationToken = default)
    {
        var memberDocs = await _chatMembersCollection
            .Find(m => m.UserId == userA || m.UserId == userB)
            .ToListAsync(cancellationToken);

        var candidateChatIds = memberDocs
            .GroupBy(m => m.ChatId)
            .Where(g => g.Any(m => m.UserId == userA) && g.Any(m => m.UserId == userB))
            .Select(g => g.Key)
            .ToList();

        if (candidateChatIds.Count == 0) return null;

        var chatDoc = await _chatsCollection
            .Find(c => candidateChatIds.Contains(c.Id) && c.Type == ChatType.Direct && c.DeletedAt == null)
            .FirstOrDefaultAsync(cancellationToken);

        if (chatDoc is null) return null;

        var allMembers = memberDocs.Where(m => m.ChatId == chatDoc.Id).ToList();
        return (DirectChat?)FromStorage(chatDoc, allMembers);
    }

    public async Task AddAsync(Chat chat, CancellationToken cancellationToken = default)
    {
        try
        {
            using var session = await mongoClient.StartSessionAsync(cancellationToken: cancellationToken);
            await session.WithTransactionAsync(async (s, token) =>
            {
                await _chatsCollection.InsertOneAsync(s, ToChatDocument(chat), cancellationToken: token);

                var memberDocs = chat.Members.Select(m => ChatMemberDocument.FromDomain(m, chat.Id)).ToList();
                if (memberDocs.Count > 0)
                    await _chatMembersCollection.InsertManyAsync(s, memberDocs, cancellationToken: token);

                await dispatcher.WriteAsync(chat.PendingEvents, s, token);
                return true;
            }, cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (IsDuplicateKey(ex))
        {
            // Idempotent retry: a chat already exists with this id (the client reused it
            // on a retry). The transaction aborted, so nothing was re-inserted and no
            // ChatCreated/MemberJoined events were re-queued to the outbox.
        }

        chat.ClearPendingEvents();
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

    public async Task UpdateAsync(Chat chat, CancellationToken cancellationToken = default)
    {
        using var session = await mongoClient.StartSessionAsync(cancellationToken: cancellationToken);
        await session.WithTransactionAsync(async (s, token) =>
        {
            await _chatsCollection.ReplaceOneAsync(
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
                await _chatMembersCollection.BulkWriteAsync(s, memberOps, cancellationToken: token);

            await dispatcher.WriteAsync(chat.PendingEvents, s, token);
            return true;
        }, cancellationToken: cancellationToken);

        chat.ClearPendingEvents();
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

    private static Chat FromStorage(ChatDocument chatDoc, List<ChatMemberDocument> memberDocs)
    {
        var members = memberDocs.Select(m => m.ToDomain()).ToList();

        return chatDoc.Type switch
        {
            ChatType.Direct => DirectChat.FromStorage(
                chatDoc.Id, chatDoc.CreatedBy, chatDoc.CreatedAt, chatDoc.DeletedAt, members),
            ChatType.Group => GroupChat.FromStorage(
                chatDoc.Id,
                ChatName.Create(chatDoc.Name ?? throw new InvalidOperationException("GroupChat must have a name.")),
                chatDoc.CreatedBy, chatDoc.CreatedAt, chatDoc.DeletedAt, members),
            ChatType.Broadcast => BroadcastChannel.FromStorage(
                chatDoc.Id,
                ChatName.Create(chatDoc.Name ?? throw new InvalidOperationException("BroadcastChannel must have a name.")),
                chatDoc.CreatedBy, chatDoc.CreatedAt, chatDoc.DeletedAt, members),
            _ => throw new InvalidOperationException($"Unknown chat type: {chatDoc.Type}")
        };
    }
}
