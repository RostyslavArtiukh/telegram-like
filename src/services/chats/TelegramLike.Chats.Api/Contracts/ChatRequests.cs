using TelegramLike.Chats.Domain.ValueObjects;

namespace TelegramLike.Chats.Api.Contracts;

public sealed record CreateDirectChatRequest(Guid PeerUserId);

public sealed record CreateGroupChatRequest(string Name);

public sealed record CreateBroadcastChannelRequest(string Name);

public sealed record ChangeMemberRoleRequest(MemberRole NewRole);

public sealed record TransferOwnershipRequest(Guid NewOwnerUserId);

public sealed record RenameChatRequest(string NewName);
