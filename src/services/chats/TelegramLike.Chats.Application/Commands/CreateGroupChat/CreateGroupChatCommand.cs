using MediatR;

namespace TelegramLike.Chats.Application.Commands.CreateGroupChat;

public sealed record CreateGroupChatCommand(Guid OwnerUserId, string Name) : IRequest<Guid>;
