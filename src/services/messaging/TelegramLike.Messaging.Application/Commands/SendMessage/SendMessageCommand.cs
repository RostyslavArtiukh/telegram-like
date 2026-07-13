using MediatR;
using TelegramLike.Messaging.Domain.ValueObjects;

namespace TelegramLike.Messaging.Application.Commands.SendMessage;

public sealed record SendMessageAttachment(AttachmentType Type, string Url, long SizeBytes, string? FileName);

public sealed record SendMessageCommand(
    // Client-supplied message id = duplicate-protection key. Empty => the handler mints one
    // (a non-idempotent send, e.g. a caller that predates this).
    Guid MessageId,
    Guid ChatId,
    Guid AuthorId,
    string? Text,
    // Server-authoritative for materialized chats ([TL-70]/[TL-102]): recipients come from the
    // membership read-model and isBroadcast from the chat-type read-model. These caller-supplied
    // values are a fallback used only while a just-created chat isn't materialized yet, then ignored.
    IReadOnlyList<Guid> Recipients,
    bool IsBroadcast,
    IReadOnlyList<SendMessageAttachment>? Attachments = null,
    Guid? ReplyToMessageId = null,
    Guid? ForwardOriginalMessageId = null,
    Guid? ForwardOriginalChatId = null) : IRequest<Guid>;
