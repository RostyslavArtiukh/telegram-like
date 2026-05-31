using MediatR;

namespace TelegramLike.Chats.Application.Commands.CreateDirectChat;

public sealed record CreateDirectChatCommand(Guid InitiatorUserId, Guid PeerUserId) : IRequest<Guid>;
