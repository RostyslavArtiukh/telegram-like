using System.Reflection;
using TelegramLike.Contracts.Common;

namespace TelegramLike.Shared.Application;

/// <summary>
/// The lookup between an integration event's stable wire name and its CLR type, built once
/// from the <see cref="IntegrationEventNameAttribute"/>s declared in Contracts.
/// </summary>
/// <remarks>
/// This is what lets a stored outbox row survive a rename: the row holds
/// <c>chats.member-joined.v1</c>, not a class name, so moving or renaming the record changes
/// nothing a queued row depends on. The wire name is declared, never derived — deriving it
/// from the type would reintroduce exactly the coupling it removes.
/// </remarks>
public static class IntegrationEventNames
{
    private static readonly Dictionary<Type, string> NameByType;
    private static readonly Dictionary<string, Type> TypeByName;

    static IntegrationEventNames()
    {
        NameByType = [];
        TypeByName = [];

        // Every integration event lives in Contracts by construction — it is the one assembly
        // both publisher and consumer share — so that assembly is the whole registry.
        var contracts = typeof(IIntegrationEvent).Assembly;

        foreach (var type in contracts.GetTypes())
        {
            if (type.IsAbstract || type.IsInterface || !typeof(IIntegrationEvent).IsAssignableFrom(type))
                continue;

            var name = type.GetCustomAttribute<IntegrationEventNameAttribute>()?.Name;
            if (name is null) continue;

            // Two types claiming one name would make resolution silently pick whichever the
            // reflection order happened to yield, and a row could deserialize into the wrong
            // shape. Fail at first touch instead — the pin test turns this into a CI failure.
            if (!TypeByName.TryAdd(name, type))
                throw new InvalidOperationException(
                    $"Integration event name '{name}' is claimed by both " +
                    $"{TypeByName[name].FullName} and {type.FullName}.");

            NameByType[type] = name;
        }
    }

    /// <summary>All declared wire names — the pin test's view of the registry.</summary>
    public static IReadOnlyDictionary<string, Type> All => TypeByName;

    /// <summary>
    /// The stable wire name to store for <paramref name="eventType"/>. Throws when the type
    /// has no declared name: an event with no identity of its own could only be stored under
    /// its CLR name, which is the coupling this exists to remove.
    /// </summary>
    public static string NameOf(Type eventType) =>
        NameByType.TryGetValue(eventType, out var name)
            ? name
            : throw new InvalidOperationException(
                $"{eventType.FullName} has no [IntegrationEventName]. Declare one " +
                "(\"context.event.v1\") so queued rows survive a rename.");

    /// <summary>
    /// Resolves a stored name back to its type, or <c>null</c> if nothing matches.
    /// </summary>
    /// <remarks>
    /// Falls back to CLR resolution for rows written before wire names existed: those hold
    /// <c>"Namespace.Type, Assembly"</c> and are still pending in real databases. The fallback
    /// is what makes this change deployable without draining the outbox first, and it stops
    /// mattering once those rows have been published or swept by the TTL.
    /// </remarks>
    public static Type? Resolve(string storedName) =>
        TypeByName.TryGetValue(storedName, out var type) ? type : Type.GetType(storedName);
}
