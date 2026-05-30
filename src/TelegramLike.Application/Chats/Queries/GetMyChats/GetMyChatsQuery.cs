using MediatR;

namespace TelegramLike.Application.Chats.Queries.GetMyChats;

public sealed record GetMyChatsQuery(Guid UserId) : IRequest<IReadOnlyList<ChatSummaryDto>>;
