namespace TelegramLike.Notifications.Api.Contracts;

/// <summary>
/// Response body for <c>GET /notifications/unread-count</c>. Serialized as <c>{ "count": &lt;n&gt; }</c>;
/// the Web BFF's Notifications client reads this exact shape (its own private mirror record).
/// </summary>
public sealed record UnreadCountResponse(long Count);
