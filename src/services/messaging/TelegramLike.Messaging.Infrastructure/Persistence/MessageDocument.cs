using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using TelegramLike.Messaging.Domain.Aggregates;
using TelegramLike.Messaging.Domain.Entities;
using TelegramLike.Messaging.Domain.ValueObjects;

namespace TelegramLike.Messaging.Infrastructure.Persistence;

internal sealed class AttachmentDocument
{
    [BsonRepresentation(BsonType.String)]
    public AttachmentType Type { get; set; }
    public string Url { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string? FileName { get; set; }

    public static AttachmentDocument FromDomain(Attachment a) => new()
    {
        Type = a.Type,
        Url = a.Url,
        SizeBytes = a.SizeBytes,
        FileName = a.FileName
    };

    public Attachment ToDomain() => Attachment.Create(Type, Url, SizeBytes, FileName);
}

internal sealed class ReactionDocument
{
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid UserId { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Emoji Emoji { get; set; }

    public DateTime AddedAt { get; set; }

    public static ReactionDocument FromDomain(Reaction r) => new()
    {
        Id = r.Id,
        UserId = r.UserId,
        Emoji = r.Emoji,
        AddedAt = r.AddedAt
    };

    public Reaction ToDomain() => Reaction.Reconstitute(Id, UserId, Emoji, AddedAt);
}

internal sealed class ForwardReferenceDocument
{
    [BsonRepresentation(BsonType.String)]
    public Guid OriginalMessageId { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid OriginalChatId { get; set; }
}

internal sealed class MessageDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid ChatId { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid AuthorId { get; set; }

    public string? Text { get; set; }

    public List<AttachmentDocument> Attachments { get; set; } = [];

    [BsonRepresentation(BsonType.String)]
    public Guid? ReplyToId { get; set; }

    public ForwardReferenceDocument? ForwardRef { get; set; }

    public List<ReactionDocument> Reactions { get; set; } = [];

    public bool IsRetracted { get; set; }
    public DateTime? RetractedAt { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid? RetractedBy { get; set; }

    public int? BroadcastReadCount { get; set; }

    public DateTime SentAt { get; set; }

    public static MessageDocument FromDomain(Message m) => new()
    {
        Id = m.Id,
        ChatId = m.ChatId,
        AuthorId = m.AuthorId,
        Text = m.Content.Text,
        Attachments = m.Content.Attachments.Select(AttachmentDocument.FromDomain).ToList(),
        ReplyToId = m.ReplyTo?.ReplyToMessageId,
        ForwardRef = m.ForwardFrom is null ? null : new ForwardReferenceDocument
        {
            OriginalMessageId = m.ForwardFrom.OriginalMessageId,
            OriginalChatId = m.ForwardFrom.OriginalChatId
        },
        Reactions = m.Reactions.Select(ReactionDocument.FromDomain).ToList(),
        IsRetracted = m.Status.IsRetracted,
        RetractedAt = m.Status.RetractedAt,
        RetractedBy = m.Status.RetractedBy,
        BroadcastReadCount = m.BroadcastReadCount,
        SentAt = m.SentAt
    };

    public Message ToDomain()
    {
        var content = MessageContent.Create(Text, Attachments.Select(a => a.ToDomain()).ToList());
        var replyRef = ReplyToId.HasValue ? ReplyReference.To(ReplyToId.Value) : null;
        var forwardRef = ForwardRef is null
            ? null
            : ForwardReference.From(ForwardRef.OriginalMessageId, ForwardRef.OriginalChatId);

        var status = IsRetracted && RetractedBy.HasValue && RetractedAt.HasValue
            ? MessageStatus.Retracted(RetractedBy.Value, RetractedAt.Value)
            : MessageStatus.Active();

        var reactions = Reactions.Select(r => r.ToDomain()).ToList();

        return Message.Reconstitute(
            Id, ChatId, AuthorId, content, replyRef, forwardRef, status, SentAt, BroadcastReadCount, reactions);
    }
}
