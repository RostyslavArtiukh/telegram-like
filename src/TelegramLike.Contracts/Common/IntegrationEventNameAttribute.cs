namespace TelegramLike.Contracts.Common;

/// <summary>
/// The stable wire name of an integration event — the identity that outlives the CLR type.
/// </summary>
/// <remarks>
/// The transactional outbox stores a type name in Mongo and resolves it back minutes, hours
/// or (for a dead-lettered row) days later. Storing the CLR name made every unpublished row
/// depend on the class never being renamed or moved: rename
/// <c>MemberJoinedIntegrationEvent</c>, or shift it to another namespace, and every row
/// already in the collection becomes unresolvable — a rollback doesn't help, because the rows
/// keep the name the old build wrote. That is a mine that goes off during ordinary
/// refactoring rather than under load.
/// <para>
/// So the name is declared here, once, and never derived from the type: <c>context.event.vN</c>
/// (kebab-case). The type may be renamed or moved freely; only this string is the contract.
/// A genuinely new shape gets a new type with <c>.v2</c> — see the versioning convention in
/// this project's CLAUDE.md — and the two coexist until every consumer has migrated.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class IntegrationEventNameAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}
