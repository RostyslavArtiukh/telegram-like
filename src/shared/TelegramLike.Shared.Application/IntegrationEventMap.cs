using TelegramLike.Contracts.Common;
using TelegramLike.Shared.Domain;

namespace TelegramLike.Shared.Application;

/// <summary>
/// Translates one service-internal <see cref="IChangeEvent"/> into the integration event
/// (from Contracts) that other services are allowed to see, or <c>null</c> when the event
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
/// </remarks>
public delegate IIntegrationEvent? IntegrationEventMap(IChangeEvent changeEvent);
