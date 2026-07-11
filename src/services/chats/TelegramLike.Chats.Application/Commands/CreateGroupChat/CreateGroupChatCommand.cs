using MediatR;

namespace TelegramLike.Chats.Application.Commands.CreateGroupChat;

// ChatId is the client-supplied duplicate-protection key; empty => the handler mints one.
public sealed record CreateGroupChatCommand(Guid ChatId, Guid OwnerUserId, string Name) : IRequest<Guid>;
