using MediatR;

namespace TelegramLike.Application.Chats.Queries.GetChatById;

public sealed record GetChatByIdQuery(Guid ChatId) : IRequest<ChatDetailsDto?>;
