using MediatR;

namespace TelegramLike.Chats.Application.Queries.GetChatById;

public sealed record GetChatByIdQuery(Guid ChatId) : IRequest<ChatDetailsDto?>;
