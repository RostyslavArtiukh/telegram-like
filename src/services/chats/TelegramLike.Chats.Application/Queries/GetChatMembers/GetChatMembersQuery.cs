using MediatR;

namespace TelegramLike.Chats.Application.Queries.GetChatMembers;

public sealed record GetChatMembersQuery(Guid ChatId, Guid RequesterId) : IRequest<IReadOnlyList<ChatMemberDto>>;
