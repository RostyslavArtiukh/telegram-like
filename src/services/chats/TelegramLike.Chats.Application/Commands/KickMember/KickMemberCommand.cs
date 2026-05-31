using MediatR;

namespace TelegramLike.Chats.Application.Commands.KickMember;

public sealed record KickMemberCommand(Guid ChatId, Guid TargetUserId, Guid ActorUserId) : IRequest;
