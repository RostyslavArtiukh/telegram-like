using System.Collections.Concurrent;

namespace TelegramLike.Realtime.Api.Membership;

/// <summary>
/// Decides whether a connection may subscribe to a chat's live events, remembering each
/// answer and keeping it fresh from the Chats events this replica already consumes.
/// </summary>
/// <remarks>
/// This used to be an eagerly replicated copy of <b>all</b> membership in the system: every
/// replica materialized every chat from the event stream, an unknown chat failed open, and a
/// restarted replica — whose temporary queue replays nothing — stayed blind until a human
/// re-ran the admin backfill. More replicas meant more of them disagreeing, and each held
/// memory proportional to the whole product rather than to its own connections ([TL-127]).
/// <para>
/// Inverted: nothing is materialized up front, an unknown pair is <i>asked about</i>, and the
/// events only correct answers already held. A fresh replica is therefore never blind, replicas
/// converge on the same authority instead of on their own event history, and what is kept in
/// memory follows the chats this replica's users actually opened.
/// </para>
/// <para>
/// <b>Events refresh, they never materialize.</b> Caching a pair nobody here asked about would
/// walk straight back into holding every membership in the system.
/// </para>
/// </remarks>
public sealed class ChatMembershipCheck(
    IChatMembershipSource source,
    ILogger<ChatMembershipCheck> logger)
{
    private readonly ConcurrentDictionary<(Guid ChatId, Guid UserId), bool> _answers = new();

    public async Task<bool> MayJoinAsync(
        Guid chatId, Guid userId, string? accessToken, CancellationToken cancellationToken = default)
    {
        if (_answers.TryGetValue((chatId, userId), out var remembered)) return remembered;

        if (string.IsNullOrEmpty(accessToken))
        {
            // The connection authenticated, so a token existed at handshake; not finding it
            // now means the transport shape changed under us and this check has no way to run.
            logger.LogWarning(
                "No access token on the connection, so membership of chat {ChatId} cannot be checked.", chatId);
            return true;
        }

        var answer = await source.IsMemberAsync(chatId, accessToken, cancellationToken);

        if (answer is null)
        {
            // Chats is unreachable. Fail open, as before — but this is now a transient,
            // logged outage rather than the permanent state of every chat a replica has not
            // happened to observe. Exposure is metadata only: pushes carry ids, and content
            // stays behind Messaging's fail-closed reads.
            logger.LogWarning(
                "Allowing {UserId} into chat {ChatId} without a membership answer — Chats could not be reached.",
                userId,
                chatId);
            return true;
        }

        _answers[(chatId, userId)] = answer.Value;
        return answer.Value;
    }

    /// <summary>Correct an answer this replica already holds. Never adds one.</summary>
    public void Refresh(Guid chatId, Guid userId, bool isMember)
    {
        if (_answers.ContainsKey((chatId, userId)))
            _answers[(chatId, userId)] = isMember;
    }

    /// <summary>
    /// The chat is gone: every answer held for it becomes "no".
    /// </summary>
    /// <remarks>
    /// Load-bearing rather than belt-and-braces — Chats' own member lookup does not consider
    /// <c>DeletedAt</c>, so asking it about a soft-deleted chat still says "yes, a member".
    /// This event is the only thing that revokes it here.
    /// </remarks>
    public void Revoke(Guid chatId)
    {
        foreach (var key in _answers.Keys)
        {
            if (key.ChatId == chatId)
                _answers[key] = false;
        }
    }

    /// <summary>Membership answers currently remembered — the memory this replica holds.</summary>
    public int RememberedAnswers => _answers.Count;
}
