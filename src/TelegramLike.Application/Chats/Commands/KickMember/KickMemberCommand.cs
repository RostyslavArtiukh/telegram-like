using MediatR;

namespace TelegramLike.Application.Chats.Commands.KickMember;

public sealed record KickMemberCommand(Guid ChatId, Guid TargetUserId, Guid ActorUserId) : IRequest;
