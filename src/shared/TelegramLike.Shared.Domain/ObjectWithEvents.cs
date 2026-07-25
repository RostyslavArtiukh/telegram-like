namespace TelegramLike.Shared.Domain;

/// <summary>
/// A domain object that, on top of an id, keeps a list of "what just happened to me"
/// events (<see cref="IChangeEvent"/>). Business methods call <see cref="RecordEvent"/>;
/// the repository reads <see cref="PendingEvents"/>, hands them to the outgoing-events
/// queue in the same database transaction, and calls <see cref="ClearPendingEvents"/>.
/// </summary>
public abstract class ObjectWithEvents : ObjectWithId
{
    private readonly List<IChangeEvent> _pendingEvents = [];

    protected ObjectWithEvents(Guid id) : base(id) { }
    protected ObjectWithEvents() { }

    public IReadOnlyList<IChangeEvent> PendingEvents => _pendingEvents.AsReadOnly();

    protected void RecordEvent(IChangeEvent changeEvent) =>
        _pendingEvents.Add(changeEvent);

    public void ClearPendingEvents() => _pendingEvents.Clear();
}
