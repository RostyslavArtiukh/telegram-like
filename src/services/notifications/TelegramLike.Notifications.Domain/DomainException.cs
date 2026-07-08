namespace TelegramLike.Notifications.Domain;

/// <summary>
/// Base type for deliberate business-rule violations raised by the domain / application layer
/// (e.g. "notification not found", "cannot mark another user's notification as read").
/// The API layer maps these to <c>400</c> and surfaces the message. Framework-thrown
/// exceptions (LINQ, the Mongo driver, config, enum-mapping default cases) are intentionally
/// <b>not</b> this type, so they bubble up as a <c>500</c> instead of being mislabelled as a
/// client <c>400</c> with an internal message leaked in the body.
/// </summary>
public class DomainException(string message) : Exception(message);
