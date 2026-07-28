using System.Collections;
using FluentAssertions;
using TelegramLike.Contracts.Messaging;
using TelegramLike.Messaging.Application.IntegrationEvents;
using TelegramLike.Messaging.Domain.Aggregates;
using TelegramLike.Messaging.Domain.Events;
using TelegramLike.Messaging.Domain.ValueObjects;
using TelegramLike.Shared.Domain;

namespace TelegramLike.Messaging.Tests.Application;

/// <summary>
/// Mirror of the Chats guard: an event with no arm in the map falls through to the default
/// and never reaches the outbox — silently. Messaging publishes everything it raises today,
/// so the exemption list is empty and must stay that way unless a reason is written down.
/// </summary>
public class MessagingIntegrationEventsTests
{
    private static readonly Dictionary<string, string> DeliberatelyInternal = new();

    private static IReadOnlyList<Type> AllChangeEventTypes() =>
        typeof(Message).Assembly
            .GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(IChangeEvent).IsAssignableFrom(t))
            .ToList();

    private static IChangeEvent Instantiate(Type changeEventType)
    {
        var ctor = changeEventType.GetConstructors().Single();
        var args = ctor.GetParameters().Select(p => DefaultFor(p.ParameterType)).ToArray();
        return (IChangeEvent)ctor.Invoke(args);
    }

    private static object? DefaultFor(Type t)
    {
        if (t == typeof(Guid)) return Guid.NewGuid();
        if (t == typeof(DateTime)) return DateTime.UtcNow;
        if (t == typeof(string)) return "x";
        if (t.IsEnum) return Enum.GetValues(t).GetValue(0);
        if (typeof(IEnumerable).IsAssignableFrom(t) && t.IsGenericType)
            return Array.CreateInstance(t.GetGenericArguments()[0], 0);
        return t.IsValueType ? Activator.CreateInstance(t) : null;
    }

    [Fact]
    public void EveryChangeEventIsEitherMappedOrDeliberatelyInternal()
    {
        var unmapped = AllChangeEventTypes()
            .Where(t => MessagingIntegrationEvents.Map(Instantiate(t)) is null)
            .Select(t => t.Name)
            .Where(name => !DeliberatelyInternal.ContainsKey(name))
            .ToList();

        unmapped.Should().BeEmpty(
            "an event with no arm falls through to the default and never reaches the outbox — "
            + "add an arm to MessagingIntegrationEvents.Map, or record why it stays internal here");
    }

    [Fact]
    public void TheDomainStillRaisesTheEventsWeExpect()
    {
        AllChangeEventTypes().Select(t => t.Name).Should().Contain(
            ["MessageSentEvent", "MessageRetractedEvent", "ReactionAddedEvent", "ReactionRemovedEvent"]);
    }

    [Fact]
    public void MessageSent_CarriesTheEmbeddedRecipients()
    {
        // Notifications fans out from this list rather than querying Chats, so losing it
        // here would silently stop every new-message notification.
        var recipients = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var source = new MessageSentEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, null, recipients);

        var mapped = MessagingIntegrationEvents.Map(source)
            .Should().BeOfType<MessageSentIntegrationEvent>().Subject;

        mapped.Recipients.Should().BeEquivalentTo(recipients);
        mapped.EventId.Should().Be(source.EventId);
        mapped.OccurredAt.Should().Be(source.OccurredAt);
    }

    [Fact]
    public void ReactionEmojiCrossesAsAString_NeverAsTheDomainEnum()
    {
        // Emoji lives in Messaging.Domain; Contracts has no project references, so the name
        // is what travels — and the SDK/BFF read it back by name.
        var added = MessagingIntegrationEvents.Map(
            new ReactionAddedEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Emoji.Fire));
        var removed = MessagingIntegrationEvents.Map(
            new ReactionRemovedEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Emoji.Heart));

        added.Should().BeOfType<ReactionAddedIntegrationEvent>().Which.Emoji.Should().Be("Fire");
        removed.Should().BeOfType<ReactionRemovedIntegrationEvent>().Which.Emoji.Should().Be("Heart");
    }
}
