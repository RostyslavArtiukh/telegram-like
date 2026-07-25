namespace TelegramLike.Shared.Domain;

/// <summary>
/// Base type for deliberate business-rule violations raised by the domain / application layer
/// (e.g. "chat not found", "only Owner or Admin can kick", "message text cannot exceed …").
/// The API layer maps these to <c>400</c> and surfaces the message. Framework-thrown exceptions
/// (LINQ, the Mongo driver, a data-integrity default case) are intentionally <b>not</b> this
/// type, so they bubble up as a <c>500</c> instead of being mislabelled as a client <c>400</c>
/// with an internal message leaked in the body.
/// </summary>
public class DomainException(string message) : Exception(message);

/// <summary>
/// A business-rule violation that denies the caller access (mapped to <c>403</c> by the API).
/// Replaces the previous raw <see cref="UnauthorizedAccessException"/> for membership checks.
/// </summary>
public sealed class ForbiddenException(string message) : DomainException(message);
