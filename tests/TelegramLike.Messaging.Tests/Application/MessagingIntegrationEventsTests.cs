using System.Collections;
using FluentAssertions;
using TelegramLike.Contracts.Common;
using TelegramLike.Contracts.Messaging;
using TelegramLike.Shared.Application;
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

    // The map returns a list because one change can fan out into several wire messages
    // ([TL-124]). Every event asserted below produces exactly one, and Single() says so.
    private static IIntegrationEvent Only(IChangeEvent changeEvent) =>
        MessagingIntegrationEvents.Map(changeEvent).Single();

    private static bool StaysInternal(IChangeEvent changeEvent) =>
        MessagingIntegrationEvents.Map(changeEvent).Count == 0;

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
            .Where(t => StaysInternal(Instantiate(t)))
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

        var mapped = Only(source).Should().BeOfType<MessageSentIntegrationEvent>().Subject;

        mapped.Recipients.Should().BeEquivalentTo(recipients);
        mapped.EventId.Should().Be(source.EventId);
        mapped.OccurredAt.Should().Be(source.OccurredAt);
        // An audience that fits in one message is still one message, unchanged.
        mapped.PartIndex.Should().Be(0);
        mapped.PartCount.Should().Be(1);
    }

    [Fact]
    public void MessageSent_SplitsALargeAudienceAcrossParts_WithoutLosingOrDuplicatingAnyone()
    {
        // The send path is where this ceiling actually bites: one event per message, embedding
        // the whole chat. Split, the parts must still add up to exactly the original audience —
        // a lost recipient here is a notification that silently never arrives.
        var recipients = Enumerable.Range(0, FanoutParts.MaxPerEvent + 1)
            .Select(_ => Guid.NewGuid())
            .ToList();
        var source = new MessageSentEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, null, recipients);

        var parts = MessagingIntegrationEvents.Map(source).Cast<MessageSentIntegrationEvent>().ToList();

        parts.Should().HaveCount(2);
        parts.Select(p => p.PartIndex).Should().Equal(0, 1);
        parts.Should().OnlyContain(p => p.PartCount == 2);
        parts.SelectMany(p => p.Recipients).Should().Equal(recipients);
        parts[0].Recipients.Should().HaveCount(FanoutParts.MaxPerEvent);

        // Every part describes the same message, so a per-recipient consumer needs no
        // awareness of parts at all...
        parts.Should().OnlyContain(p => p.MessageId == source.MessageId && p.ChatId == source.ChatId);
        // ...but their ids differ, because Notifications deduplicates by (recipient, source
        // event) and a shared id would be indistinguishable from a redelivery.
        parts.Select(p => p.EventId).Should().OnlyHaveUniqueItems();
        parts[0].EventId.Should().Be(source.EventId);
    }

    [Fact]
    public void MessageSent_ToAChatWithNobodyElse_StillTravels()
    {
        // The unmaterialized-chat case from [TL-118]: the message is stored and readable, it
        // just has no audience. Dropping it here would also drop the realtime push.
        var source = new MessageSentEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, null, []);

        var mapped = Only(source).Should().BeOfType<MessageSentIntegrationEvent>().Subject;

        mapped.Recipients.Should().BeEmpty();
        mapped.PartCount.Should().Be(1);
    }

    [Fact]
    public void ReactionEmojiCrossesAsAString_NeverAsTheDomainEnum()
    {
        // Emoji lives in Messaging.Domain; Contracts has no project references, so the name
        // is what travels — and the SDK/BFF read it back by name.
        var added = Only(new ReactionAddedEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Emoji.Fire));
        var removed = Only(new ReactionRemovedEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Emoji.Heart));

        added.Should().BeOfType<ReactionAddedIntegrationEvent>().Which.Emoji.Should().Be("Fire");
        removed.Should().BeOfType<ReactionRemovedIntegrationEvent>().Which.Emoji.Should().Be("Heart");
    }
}
