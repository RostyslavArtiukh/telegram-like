namespace TelegramLike.Messaging.Api.Contracts;

/// <summary>
/// Returned by <c>POST /messages/</c> (201 Created). The Web BFF reads <c>messageId</c> off
/// this body, so the property name must stay exactly as-is.
/// </summary>
public sealed record MessageCreatedResponse(Guid MessageId);
