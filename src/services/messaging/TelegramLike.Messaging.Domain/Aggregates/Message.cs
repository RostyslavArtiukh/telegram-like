using TelegramLike.Shared.Domain;
using TelegramLike.Messaging.Domain.Entities;
using TelegramLike.Messaging.Domain.Events;
using TelegramLike.Messaging.Domain.ValueObjects;

namespace TelegramLike.Messaging.Domain.Aggregates;

public sealed class Message : ObjectWithEvents
{
    public const int FreeUserReactionLimit = 1;
    public const int PremiumUserReactionLimit = 2;

    private readonly List<Reaction> _reactions = [];

    public Guid ChatId { get; private set; }
    public Guid AuthorId { get; private set; }
    public MessageContent Content { get; private set; } = null!;
    public ReplyReference? ReplyTo { get; private set; }
    public ForwardReference? ForwardFrom { get; private set; }
    public MessageStatus Status { get; private set; } = null!;
    public DateTime SentAt { get; private set; }
    public int? BroadcastReadCount { get; private set; }

    // Optimistic-concurrency token. The repository guards a whole-document write on
    // this value and increments it, so two concurrent reaction/retract writers can't
    // silently clobber each other via a last-write-wins ReplaceOne.
    public int Version { get; private set; }

    public IReadOnlyList<Reaction> Reactions => _reactions.AsReadOnly();
    public bool IsRetracted => Status.IsRetracted;

    // A broadcast message tracks an aggregate read counter (non-null); Direct/Group messages
    // do not. Lets read-side callers derive broadcast-ness from the message itself instead of
    // trusting a client-supplied flag ([TL-102]).
    public bool IsBroadcast => BroadcastReadCount is not null;

    private Message() { }

    private Message(
        Guid id,
        Guid chatId,
        Guid authorId,
        MessageContent content,
        ReplyReference? replyTo,
        ForwardReference? forwardFrom,
        MessageStatus status,
        DateTime sentAt,
        int? broadcastReadCount,
        int version = 0)
        : base(id)
    {
        ChatId = chatId;
        AuthorId = authorId;
        Content = content;
        ReplyTo = replyTo;
        ForwardFrom = forwardFrom;
        Status = status;
        SentAt = sentAt;
        BroadcastReadCount = broadcastReadCount;
        Version = version;
    }

    public static Message Send(
        Guid messageId,
        Guid chatId,
        Guid authorId,
        MessageContent content,
        IReadOnlyList<Guid> recipients,
        ReplyReference? replyTo = null,
        ForwardReference? forwardFrom = null,
        bool isBroadcast = false)
    {
        // Caller-supplied id doubles as the duplicate-protection key: a retried send reuses the
        // same id, so the unique _id insert dedupes it (see MessageRepository.AddAsync).
        if (messageId == Guid.Empty) throw new DomainException("MessageId cannot be empty.");
        if (chatId == Guid.Empty) throw new DomainException("ChatId cannot be empty.");
        if (authorId == Guid.Empty) throw new DomainException("AuthorId cannot be empty.");
        ArgumentNullException.ThrowIfNull(recipients);

        var message = new Message(
            messageId,
            chatId,
            authorId,
            content,
            replyTo,
            forwardFrom,
            MessageStatus.Active(),
            DateTime.UtcNow,
            isBroadcast ? 0 : null);

        message.RecordEvent(new MessageSentEvent(
            message.Id, chatId, authorId,
            replyTo?.ReplyToMessageId,
            forwardFrom?.OriginalMessageId,
            recipients));

        return message;
    }

    public static Message FromStorage(
        Guid id,
        Guid chatId,
        Guid authorId,
        MessageContent content,
        ReplyReference? replyTo,
        ForwardReference? forwardFrom,
        MessageStatus status,
        DateTime sentAt,
        int? broadcastReadCount,
        IEnumerable<Reaction> reactions,
        int version = 0)
    {
        var message = new Message(id, chatId, authorId, content, replyTo, forwardFrom, status, sentAt, broadcastReadCount, version);
        message._reactions.AddRange(reactions);
        return message;
    }

    public void Retract(Guid retractedBy, bool isAuthorOrModerator)
    {
        EnsureNotRetracted();

        if (!isAuthorOrModerator)
            throw new DomainException("Only the author, an Admin, or the Owner can retract a message.");

        Status = MessageStatus.Retracted(retractedBy, DateTime.UtcNow);
        Content = MessageContent.Create("[retracted]");
        RecordEvent(new MessageRetractedEvent(Id, ChatId, retractedBy));
    }

    public void AddReaction(Guid userId, Emoji emoji, bool isPremium)
    {
        EnsureNotRetracted();

        var existing = _reactions.Where(r => r.UserId == userId).ToList();

        if (existing.Any(r => r.Emoji == emoji))
            throw new DomainException("User has already reacted with this emoji.");

        var limit = isPremium ? PremiumUserReactionLimit : FreeUserReactionLimit;
        if (existing.Count >= limit)
            throw new DomainException(
                $"User has reached the maximum number of reactions ({limit}) for this message.");

        _reactions.Add(Reaction.Add(userId, emoji));
        RecordEvent(new ReactionAddedEvent(Id, ChatId, userId, emoji));
    }

    public void RemoveReaction(Guid userId, Emoji emoji)
    {
        var reaction = _reactions.FirstOrDefault(r => r.UserId == userId && r.Emoji == emoji)
            ?? throw new DomainException("Reaction not found.");

        _reactions.Remove(reaction);
        RecordEvent(new ReactionRemovedEvent(Id, ChatId, userId, emoji));
    }

    public void IncrementBroadcastReadCount()
    {
        if (BroadcastReadCount is null)
            throw new DomainException("Broadcast read count is only available for BroadcastChannel messages.");

        BroadcastReadCount++;
    }

    private void EnsureNotRetracted()
    {
        if (IsRetracted)
            throw new DomainException("Cannot operate on a retracted message.");
    }
}
