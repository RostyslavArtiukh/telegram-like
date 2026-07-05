using System.Text.Json.Serialization;

namespace TelegramLike.Client.Chats;

// Client-local enums mirror Chats.Domain (Direct/Group/Broadcast etc.) so the SDK
// does not have to reference Chats.Domain. JSON arrives as strings thanks to
// JsonStringEnumConverter on Chats.Api.
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ChatTypeContract { Direct, Group, Broadcast }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MemberRoleContract { Owner, Admin, Member, Viewer }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MemberStatusContract { Active, Left, Kicked, Banned }

public sealed record ChatSummaryContract(
    Guid ChatId,
    ChatTypeContract Type,
    string? Name,
    MemberRoleContract MyRole,
    int ActiveMemberCount);

public sealed record ChatMemberContract(
    Guid UserId,
    MemberRoleContract Role,
    MemberStatusContract Status,
    DateTime JoinedAt,
    DateTime? LeftAt);

public sealed record ChatDetailsContract(
    Guid ChatId,
    ChatTypeContract Type,
    string? Name,
    Guid CreatedBy,
    DateTime CreatedAt,
    bool IsDeleted,
    IReadOnlyList<ChatMemberContract> Members);
