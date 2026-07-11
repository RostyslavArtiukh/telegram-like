using MediatR;

namespace TelegramLike.Chats.Application.Commands.CreateDirectChat;

// ChatId is the client-supplied duplicate-protection key; empty => the handler mints one.
public sealed record CreateDirectChatCommand(Guid ChatId, Guid InitiatorUserId, Guid PeerUserId) : IRequest<Guid>;
