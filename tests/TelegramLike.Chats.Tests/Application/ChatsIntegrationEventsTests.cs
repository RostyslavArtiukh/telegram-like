using System.Collections;
using FluentAssertions;
using TelegramLike.Chats.Application.IntegrationEvents;
using TelegramLike.Chats.Domain.Aggregates;
using TelegramLike.Chats.Domain.Events;
using TelegramLike.Chats.Domain.ValueObjects;
using TelegramLike.Contracts.Chats;
using TelegramLike.Contracts.Common;
using TelegramLike.Shared.Application;
using TelegramLike.Shared.Domain;

namespace TelegramLike.Chats.Tests.Application;

/// <summary>
/// The map is the only thing standing between a domain change event and the wire. An event
/// that falls through to the default arm is kept inside the service — silently, by design —
/// so the set of arms has to be pinned, and so does the shape each one produces.
/// </summary>
public class ChatsIntegrationEventsTests
{
    /// <summary>
    /// Change events that deliberately never leave Chats. Adding a name here must come with a
    /// reason; removing one must come with an arm in <see cref="ChatsIntegrationEvents.Map"/>.
    /// </summary>
    private static readonly Dictionary<string, string> DeliberatelyInternal = new()
    {
        ["ChatRenamedEvent"] =
            "No service stores chat names — Messaging keeps chat types, Presence and Realtime keep "
            + "memberships. Publishing it would add outbox rows and bus traffic with no consumer. "
            + "If a live chat-list rename push is ever wanted, add the arm AND a consumer together.",
        ["OwnershipTransferredEvent"] =
            "Redundant: TransferOwnership also raises the two MemberRoleChangedEvents that already "
            + "carry the new roles to the read-models. This one is a Chats-internal audit record.",
    };

    // The map returns a list because one change can fan out into several wire messages
    // ([TL-124]). Every event asserted below produces exactly one, and Single() says so.
    private static IIntegrationEvent Only(IChangeEvent changeEvent) =>
        ChatsIntegrationEvents.Map(changeEvent).Single();

    private static bool StaysInternal(IChangeEvent changeEvent) =>
        ChatsIntegrationEvents.Map(changeEvent).Count == 0;

    private static IReadOnlyList<Type> AllChangeEventTypes() =>
        typeof(Chat).Assembly
            .GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(IChangeEvent).IsAssignableFrom(t))
            .ToList();

    // Change events are positional records of ids, enums, strings and id lists, so a
    // per-parameter default is enough to build one of every type without naming them all.
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
            + "add an arm to ChatsIntegrationEvents.Map, or record why it stays internal here");
    }

    [Fact]
    public void TheDeliberatelyInternalListHasNoStaleEntries()
    {
        // An entry that now has an arm (or no longer names a real event) would quietly widen
        // the exemption for a future event of the same name.
        var actuallyInternal = AllChangeEventTypes()
            .Where(t => StaysInternal(Instantiate(t)))
            .Select(t => t.Name)
            .ToHashSet();

        DeliberatelyInternal.Keys.Should().BeSubsetOf(actuallyInternal);
    }

    [Fact]
    public void TheDomainStillRaisesTheEventsWeExpect()
    {
        // Stops the tests above passing trivially if the events were renamed or moved away.
        AllChangeEventTypes().Select(t => t.Name).Should().Contain(
            ["ChatCreatedEvent", "ChatDeletedEvent", "MemberBannedEvent", "MemberJoinedEvent", "ChatRenamedEvent"]);
    }

    // ── The shapes that actually cross the boundary ───────────────────────

    [Fact]
    public void MemberRoleChanged_PublishesOnlyTheNewRole_NotWhoChangedIt()
    {
        // The narrowing that justifies having a translation step at all: internally the event
        // carries OldRole and ChangedBy, and neither belongs on a bus other services read.
        var chatId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var source = new MemberRoleChangedEvent(chatId, userId, MemberRole.Member, MemberRole.Admin, Guid.NewGuid());

        var mapped = Only(source).Should().BeOfType<MemberRoleChangedIntegrationEvent>().Subject;

        mapped.ChatId.Should().Be(chatId);
        mapped.UserId.Should().Be(userId);
        mapped.Role.Should().Be("Admin");
        mapped.GetType().GetProperties().Select(p => p.Name)
            .Should().NotContain(["OldRole", "ChangedBy"]);
    }

    [Fact]
    public void DomainEnumsCrossAsStrings_NeverAsDomainTypes()
    {
        // Contracts has no project references on purpose (it ships in the client SDK), so a
        // domain enum has to become a string here or it could not be referenced at all.
        var created = Only(new ChatCreatedEvent(Guid.NewGuid(), ChatType.Broadcast, Guid.NewGuid()));
        var joined = Only(new MemberJoinedEvent(Guid.NewGuid(), Guid.NewGuid(), MemberRole.Viewer, []));

        created.Should().BeOfType<ChatCreatedIntegrationEvent>().Which.Type.Should().Be("Broadcast");
        joined.Should().BeOfType<MemberJoinedIntegrationEvent>().Which.Role.Should().Be("Viewer");
    }

    [Fact]
    public void MemberBanned_CarriesActorAndReason()
    {
        var chatId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var bannedBy = Guid.NewGuid();

        var mapped = Only(new MemberBannedEvent(chatId, userId, bannedBy, "spam"))
            .Should().BeOfType<MemberBannedIntegrationEvent>().Subject;

        mapped.ChatId.Should().Be(chatId);
        mapped.UserId.Should().Be(userId);
        mapped.BannedBy.Should().Be(bannedBy);
        mapped.Reason.Should().Be("spam");
    }

    [Fact]
    public void IdentityAndTimestampAreCarriedThrough_SoConsumersCanDedupe()
    {
        // Consumers dedupe on EventId and resolve last-writer-wins on OccurredAt; minting
        // fresh ones here would break idempotency on redelivery.
        var source = new MemberLeftEvent(Guid.NewGuid(), Guid.NewGuid());

        var mapped = Only(source);

        mapped.EventId.Should().Be(source.EventId);
        mapped.OccurredAt.Should().Be(source.OccurredAt);
    }

    [Fact]
    public void MemberJoined_SplitsALargeAudienceAcrossParts_WithoutLosingOrDuplicatingAnyone()
    {
        // A join announcement embeds everyone to notify, so its size follows the size of the
        // chat. Split, the parts must still add up to exactly the original audience.
        var recipients = Enumerable.Range(0, FanoutParts.MaxPerEvent * 2 + 1)
            .Select(_ => Guid.NewGuid())
            .ToList();
        var source = new MemberJoinedEvent(Guid.NewGuid(), Guid.NewGuid(), MemberRole.Member, recipients);

        var parts = ChatsIntegrationEvents.Map(source).Cast<MemberJoinedIntegrationEvent>().ToList();

        parts.Should().HaveCount(3);
        parts.Select(p => p.PartIndex).Should().Equal(0, 1, 2);
        parts.Should().OnlyContain(p => p.PartCount == 3);
        parts.SelectMany(p => p.Recipients).Should().Equal(recipients);

        // Every part repeats the fields that describe the join itself...
        parts.Should().OnlyContain(p => p.UserId == source.UserId && p.Role == "Member");
        // ...but each carries a distinct id, because Notifications deduplicates by
        // (recipient, source event) and a shared id would look like a redelivery.
        parts.Select(p => p.EventId).Should().OnlyHaveUniqueItems();
        parts[0].EventId.Should().Be(source.EventId);
    }
}
