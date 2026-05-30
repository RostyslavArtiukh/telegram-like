using MediatR;

namespace TelegramLike.Application.Chats.Queries.GetChatMembers;

public sealed record GetChatMembersQuery(Guid ChatId) : IRequest<IReadOnlyList<ChatMemberDto>>;
