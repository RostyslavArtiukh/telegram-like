using MediatR;

namespace TelegramLike.Application.Chats.Commands.CreateDirectChat;

public sealed record CreateDirectChatCommand(Guid InitiatorUserId, Guid PeerUserId) : IRequest<Guid>;
