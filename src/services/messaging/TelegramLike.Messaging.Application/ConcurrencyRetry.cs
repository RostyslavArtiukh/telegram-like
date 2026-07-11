using TelegramLike.Domain.ServiceDefaults;

namespace TelegramLike.Messaging.Application;

/// <summary>
/// Runs a load-mutate-save action, retrying on optimistic-concurrency conflicts.
/// The action must re-load the aggregate each attempt so it re-applies onto the
/// latest version. Domain exceptions thrown by the mutation propagate immediately
/// (they are not concurrency conflicts and must not be retried).
/// </summary>
public static class ConcurrencyRetry
{
    public static async Task ExecuteAsync(Func<Task> action, int maxAttempts = 4)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await action();
                return;
            }
            catch (ConcurrencyConflictException) when (attempt < maxAttempts)
            {
                // Another writer won the race; reload and retry.
            }
        }
    }
}
