namespace TelegramLike.Domain.ServiceDefaults;

/// <summary>
/// Base class for domain objects that are identified by an <see cref="Id"/>:
/// two instances are "the same thing" when their ids match, no matter what
/// their other fields currently hold.
/// </summary>
public abstract class ObjectWithId
{
    public Guid Id { get; protected set; }

    protected ObjectWithId(Guid id) => Id = id;
    protected ObjectWithId() { }

    public override bool Equals(object? obj)
    {
        if (obj is not ObjectWithId other) return false;
        if (ReferenceEquals(this, other)) return true;
        if (GetType() != other.GetType()) return false;
        return Id == other.Id;
    }

    public override int GetHashCode() => Id.GetHashCode();

    public static bool operator ==(ObjectWithId? left, ObjectWithId? right) =>
        left is not null && left.Equals(right);

    public static bool operator !=(ObjectWithId? left, ObjectWithId? right) => !(left == right);
}
