using MediatR;
using TelegramLike.Domain.Messaging.ValueObjects;

namespace TelegramLike.Application.Messaging.Commands.SendMessage;

public sealed record SendMessageAttachment(AttachmentType Type, string Url, long SizeBytes, string? FileName);

public sealed record SendMessageCommand(
    Guid ChatId,
    Guid AuthorId,
    string? Text,
    IReadOnlyList<SendMessageAttachment>? Attachments = null,
    Guid? ReplyToMessageId = null,
    Guid? ForwardOriginalMessageId = null,
    Guid? ForwardOriginalChatId = null) : IRequest<Guid>;
