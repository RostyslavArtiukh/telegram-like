using System.Diagnostics.Metrics;

namespace TelegramLike.Messaging.Application.Observability;

/// <summary>
/// Product-level counters for messaging. These answer "is the app being used, and how",
/// which the HTTP metrics can't: a healthy request rate with zero messages sent is a
/// broken product, and the RED dashboard would show both as green.
/// </summary>
public sealed class MessagingMetrics : IDisposable
{
    public const string MeterName = "TelegramLike.Messaging";

    private readonly Meter _meter;
    private readonly Counter<long> _messagesSent;
    private readonly Counter<long> _reactionsAdded;
    private readonly Counter<long> _messagesRetracted;

    public MessagingMetrics()
    {
        _meter = new Meter(MeterName);

        _messagesSent = _meter.CreateCounter<long>(
            "telegramlike.messages.sent",
            unit: "{message}",
            description: "Messages accepted by the service.");

        _reactionsAdded = _meter.CreateCounter<long>(
            "telegramlike.reactions.added",
            unit: "{reaction}",
            description: "Reactions added to a message.");

        _messagesRetracted = _meter.CreateCounter<long>(
            "telegramlike.messages.retracted",
            unit: "{message}",
            description: "Messages retracted by their author or a moderator.");
    }

    /// <param name="kind">"new", "reply" or "forward" — kept to three values on purpose.</param>
    public void RecordMessageSent(bool isBroadcast, string kind) =>
        _messagesSent.Add(
            1,
            new KeyValuePair<string, object?>("broadcast", isBroadcast),
            new KeyValuePair<string, object?>("kind", kind));

    public void RecordReactionAdded() => _reactionsAdded.Add(1);

    public void RecordMessageRetracted(bool byModerator) =>
        _messagesRetracted.Add(1, new KeyValuePair<string, object?>("by_moderator", byModerator));

    public void Dispose() => _meter.Dispose();
}
