using MediatR;
using TelegramLike.Chats.Domain.ValueObjects;

namespace TelegramLike.Chats.Application.Commands.ChangeMemberRole;

public sealed record ChangeMemberRoleCommand(
    Guid ChatId,
    Guid TargetUserId,
    MemberRole NewRole,
    Guid ActorUserId) : IRequest;
