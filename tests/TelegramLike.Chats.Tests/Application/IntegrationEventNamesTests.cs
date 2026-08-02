using System.Reflection;
using FluentAssertions;
using TelegramLike.Contracts.Chats;
using TelegramLike.Contracts.Common;
using TelegramLike.Shared.Application;

namespace TelegramLike.Chats.Tests.Application;

/// <summary>
/// Pins the wire-name registry every queued outbox row depends on. It covers all of Contracts
/// rather than just this service's events, and lives here for the same reason
/// <c>OutgoingEventsWriterTests</c> does — the shared outbox has no test project of its own.
/// </summary>
public class IntegrationEventNamesTests
{
    private static IEnumerable<Type> AllIntegrationEvents =>
        typeof(IIntegrationEvent).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface && typeof(IIntegrationEvent).IsAssignableFrom(t));

    [Fact]
    public void EveryIntegrationEventDeclaresAWireName()
    {
        // The writer throws on an unnamed event, which would mean discovering the omission
        // when a real publish fails. This is the same check, one CI run earlier.
        var unnamed = AllIntegrationEvents
            .Where(t => t.GetCustomAttribute<IntegrationEventNameAttribute>() is null)
            .Select(t => t.FullName)
            .ToList();

        unnamed.Should().BeEmpty(
            "every integration event needs an [IntegrationEventName(\"context.event.v1\")] — " +
            "without one it can only be stored under its CLR name, which breaks on rename");
    }

    [Fact]
    public void WireNamesFollowTheContextEventVersionConvention()
    {
        var malformed = IntegrationEventNames.All.Keys
            .Where(name => !System.Text.RegularExpressions.Regex.IsMatch(name, @"^[a-z]+(\.[a-z0-9-]+)+\.v\d+$"))
            .ToList();

        malformed.Should().BeEmpty("wire names are lowercase 'context.event.vN'");
    }

    [Fact]
    public void EveryDeclaredNameResolvesBackToItsType()
    {
        foreach (var (name, type) in IntegrationEventNames.All)
        {
            IntegrationEventNames.Resolve(name).Should().Be(type);
            IntegrationEventNames.NameOf(type).Should().Be(name);
        }
    }

    [Fact]
    public void ResolveStillLoadsLegacyClrNamedRows()
    {
        // Rows written before wire names existed are still pending in real databases; a
        // deploy that couldn't read them would strand every one until it dead-lettered.
        IntegrationEventNames
            .Resolve("TelegramLike.Contracts.Chats.MemberLeftIntegrationEvent, TelegramLike.Contracts")
            .Should().Be(typeof(MemberLeftIntegrationEvent));
    }

    [Fact]
    public void ResolveReturnsNullForAnUnknownName()
    {
        IntegrationEventNames.Resolve("chats.nothing-like-this.v9").Should().BeNull();
    }

    [Fact]
    public void NameOfRejectsATypeWithNoDeclaredName()
    {
        var act = () => IntegrationEventNames.NameOf(typeof(UnnamedEvent));

        act.Should().Throw<InvalidOperationException>().WithMessage("*IntegrationEventName*");
    }

    private sealed record UnnamedEvent(Guid EventId, DateTime OccurredAt) : IIntegrationEvent;
}
