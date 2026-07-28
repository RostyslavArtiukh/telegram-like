using MediatR;

namespace TelegramLike.Chats.Application.Commands.BanMember;

public sealed record BanMemberCommand(
    Guid ChatId,
    Guid MemberToBanUserId,
    Guid BannedByUserId,
    string? Reason) : IRequest;
