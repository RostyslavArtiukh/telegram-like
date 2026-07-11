using MediatR;
using TelegramLike.Chats.Domain.ValueObjects;

namespace TelegramLike.Chats.Application.Commands.ChangeMemberRole;

public sealed record ChangeMemberRoleCommand(
    Guid ChatId,
    Guid MemberToChangeUserId,
    MemberRole NewRole,
    Guid ChangedByUserId) : IRequest;
