using MediatR;
using TelegramLike.Messaging.Domain.ValueObjects;

namespace TelegramLike.Messaging.Application.Commands.SendMessage;

public sealed record SendMessageAttachment(AttachmentType Type, string Url, long SizeBytes, string? FileName);

public sealed record SendMessageCommand(
    // Client-supplied message id = idempotency key. Empty => the handler mints one
    // (a non-idempotent send, e.g. a caller that predates this).
    Guid MessageId,
    Guid ChatId,
    Guid AuthorId,
    string? Text,
    // Web BFF resolves these by querying ChatsApi before calling Messaging.
    // Chats lives in its own service now, so Messaging can't look them up.
    IReadOnlyList<Guid> Recipients,
    bool IsBroadcast,
    IReadOnlyList<SendMessageAttachment>? Attachments = null,
    Guid? ReplyToMessageId = null,
    Guid? ForwardOriginalMessageId = null,
    Guid? ForwardOriginalChatId = null) : IRequest<Guid>;
