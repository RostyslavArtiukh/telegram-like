using TelegramLike.Chats.Domain.ValueObjects;

namespace TelegramLike.Chats.Api.Contracts;

// ChatId is the client-generated duplicate-protection key. Empty/absent => the service mints
// one. A retried create reuses it so the chat isn't duplicated.
public sealed record CreateDirectChatRequest(Guid PeerUserId, Guid ChatId = default);

public sealed record CreateGroupChatRequest(string Name, Guid ChatId = default);

public sealed record CreateBroadcastChannelRequest(string Name, Guid ChatId = default);

public sealed record ChangeMemberRoleRequest(MemberRole NewRole);

public sealed record TransferOwnershipRequest(Guid NewOwnerUserId);

public sealed record RenameChatRequest(string NewName);
