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
    // Recipients are not here on purpose ([TL-118]): they are derived from the membership
    // read-model, never supplied. IsBroadcast is still a caller fallback, used only while the
    // chat-type read-model hasn't materialized the chat ([TL-102]) — unlike a missed fan-out,
    // guessing broadcast-ness wrong is baked into the stored message permanently.
    bool IsBroadcast,
    IReadOnlyList<SendMessageAttachment>? Attachments = null,
    Guid? ReplyToMessageId = null,
    Guid? ForwardOriginalMessageId = null,
    Guid? ForwardOriginalChatId = null) : IRequest<Guid>;
