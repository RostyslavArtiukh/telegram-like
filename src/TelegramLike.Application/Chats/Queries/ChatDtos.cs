using TelegramLike.Domain.Chats.ValueObjects;

namespace TelegramLike.Application.Chats.Queries;

public sealed record ChatSummaryDto(
    Guid ChatId,
    ChatType Type,
    string? Name,
    MemberRole MyRole,
    int ActiveMemberCount);

public sealed record ChatMemberDto(
    Guid UserId,
    MemberRole Role,
    MemberStatus Status,
    DateTime JoinedAt,
    DateTime? LeftAt);

public sealed record ChatDetailsDto(
    Guid ChatId,
    ChatType Type,
    string? Name,
    Guid CreatedBy,
    DateTime CreatedAt,
    bool IsDeleted,
    IReadOnlyList<ChatMemberDto> Members);
