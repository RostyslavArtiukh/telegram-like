using MediatR;

namespace TelegramLike.Messaging.Application.Commands.MarkMessageAsRead;

public sealed record MarkMessageAsReadCommand(
    Guid MessageId,
    Guid ReaderUserId,
    // Web BFF tells us whether this is a BroadcastChannel chat — otherwise we
    // would have to ask Chats for the chat type.
    bool IsBroadcast) : IRequest;
