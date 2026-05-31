using MediatR;

namespace TelegramLike.Chats.Application.Commands.JoinChat;

public sealed record JoinChatCommand(Guid ChatId, Guid UserId) : IRequest;
