using MediatR;

namespace TelegramLike.Chats.Application.Queries.GetChatMembers;

public sealed record GetChatMembersQuery(Guid ChatId) : IRequest<IReadOnlyList<ChatMemberDto>>;
