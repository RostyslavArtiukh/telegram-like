using System.Text.Json.Serialization;

namespace TelegramLike.Client.Chats;

// Client-local enums mirror Chats.Domain (Direct/Group/Broadcast etc.) so the SDK
// does not have to reference Chats.Domain. JSON arrives as strings thanks to
// JsonStringEnumConverter on Chats.Api.
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ChatType { Direct, Group, Broadcast }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MemberRole { Owner, Admin, Member, Viewer }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MemberStatus { Active, Left, Kicked, Banned }

public sealed record ChatSummary(
    Guid ChatId,
    ChatType Type,
    string? Name,
    MemberRole MyRole,
    int ActiveMemberCount);

public sealed record ChatMember(
    Guid UserId,
    MemberRole Role,
    MemberStatus Status,
    DateTime JoinedAt,
    DateTime? LeftAt);

public sealed record ChatDetails(
    Guid ChatId,
    ChatType Type,
    string? Name,
    Guid CreatedBy,
    DateTime CreatedAt,
    bool IsDeleted,
    IReadOnlyList<ChatMember> Members);
