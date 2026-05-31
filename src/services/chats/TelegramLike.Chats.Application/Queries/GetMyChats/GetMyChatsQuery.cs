using MediatR;

namespace TelegramLike.Chats.Application.Queries.GetMyChats;

public sealed record GetMyChatsQuery(Guid UserId) : IRequest<IReadOnlyList<ChatSummaryDto>>;
