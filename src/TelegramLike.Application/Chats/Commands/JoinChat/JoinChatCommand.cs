using MediatR;

namespace TelegramLike.Application.Chats.Commands.JoinChat;

public sealed record JoinChatCommand(Guid ChatId, Guid UserId) : IRequest;
