using TelegramLike.Contracts.Common;
using TelegramLike.Shared.Domain;

namespace TelegramLike.Shared.Application;

/// <summary>
/// Translates one service-internal <see cref="IChangeEvent"/> into the integration events
/// (from Contracts) that other services are allowed to see. An empty result means the event
/// deliberately stays inside this service.
/// </summary>
/// <remarks>
/// The translation step is load-bearing, the plumbing around it was not. <c>Contracts</c> has
/// no project references on purpose — it ships inside the NuGet-packable client SDK — so a
/// domain type like <c>MemberRole</c> can never appear on the wire, and the outbox replays a
/// stored payload long after the fact, meaning a domain rename must stop at this seam.
/// <para>
/// One delegate per service replaces the former one-class-per-event-type registry: every
/// published event is visible in a single switch, so an event that stays internal is an
/// explicit arm rather than a missing DI line that silently drops it.
/// </para>
/// <para>
/// The result is a list rather than a single event because one change can be more than one wire
/// message: an audience too large to embed in one event is split into parts here, at the seam
/// that already owns "what this looks like on the wire". See <see cref="FanoutParts"/>.
/// </para>
/// </remarks>
public delegate IReadOnlyList<IIntegrationEvent> IntegrationEventMap(IChangeEvent changeEvent);
