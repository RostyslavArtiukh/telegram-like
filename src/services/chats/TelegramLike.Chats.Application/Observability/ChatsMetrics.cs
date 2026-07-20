using System.Diagnostics.Metrics;

namespace TelegramLike.Chats.Application.Observability;

/// <summary>
/// Product-level counters for chats: how many conversations get created and how
/// membership churns. Paired with messaging's counters these show whether traffic is
/// real usage or just health checks.
/// </summary>
public sealed class ChatsMetrics : IDisposable
{
    public const string MeterName = "TelegramLike.Chats";

    private readonly Meter _meter;
    private readonly Counter<long> _chatsCreated;
    private readonly Counter<long> _membershipChanges;

    public ChatsMetrics()
    {
        _meter = new Meter(MeterName);

        _chatsCreated = _meter.CreateCounter<long>(
            "telegramlike.chats.created",
            unit: "{chat}",
            description: "Chats created, by kind.");

        _membershipChanges = _meter.CreateCounter<long>(
            "telegramlike.chat.membership.changes",
            unit: "{change}",
            description: "Members joining, leaving or being kicked.");
    }

    /// <param name="kind">"direct", "group" or "broadcast".</param>
    public void RecordChatCreated(string kind) =>
        _chatsCreated.Add(1, new KeyValuePair<string, object?>("kind", kind));

    /// <param name="change">"joined", "left" or "kicked".</param>
    public void RecordMembershipChange(string change) =>
        _membershipChanges.Add(1, new KeyValuePair<string, object?>("change", change));

    public void Dispose() => _meter.Dispose();
}
