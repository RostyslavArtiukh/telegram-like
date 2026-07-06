namespace TelegramLike.Messaging.Domain.Common;

/// <summary>
/// Thrown when an optimistic-concurrency guarded write finds the aggregate was
/// modified by another writer since it was loaded (version mismatch). Callers reload
/// and retry the load-mutate-save; it is not surfaced to the API.
/// </summary>
public sealed class ConcurrencyConflictException(string message) : Exception(message);
