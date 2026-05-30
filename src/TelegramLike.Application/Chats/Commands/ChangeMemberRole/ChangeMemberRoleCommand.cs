using MediatR;
using TelegramLike.Domain.Chats.ValueObjects;

namespace TelegramLike.Application.Chats.Commands.ChangeMemberRole;

public sealed record ChangeMemberRoleCommand(
    Guid ChatId,
    Guid TargetUserId,
    MemberRole NewRole,
    Guid ActorUserId) : IRequest;
