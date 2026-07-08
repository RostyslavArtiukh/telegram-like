namespace TelegramLike.Identity.Domain;

/// <summary>
/// Base type for deliberate business-rule violations raised by the domain / application layer
/// (e.g. "invalid email or password", "email is already taken", "cannot block yourself").
/// The API layer maps these to <c>400</c> and surfaces the message (as the legacy
/// <c>{ error }</c> body the Web BFF reads). Framework-thrown exceptions (LINQ, the Mongo driver,
/// startup configuration) are intentionally <b>not</b> this type, so they bubble up as a
/// <c>500</c> instead of being mislabelled as a client <c>400</c>.
/// <para>
/// Note: Identity deliberately does <b>not</b> convert its <see cref="ArgumentException"/> value-object
/// guards — those keep their previous behaviour (unmapped → 500), matching the pre-refactor contract.
/// </para>
/// </summary>
public class DomainException(string message) : Exception(message);
