namespace TelegramLike.Realtime.Api.Membership;

/// <summary>
/// The authority on "is this user in this chat", asked on behalf of the user themselves.
/// </summary>
public interface IChatMembershipSource
{
    /// <returns>
    /// <c>true</c>/<c>false</c> for a definite answer, or <c>null</c> when the question could
    /// not be asked at all (Chats unreachable, timed out, answered with an error). The caller
    /// has to treat "don't know" differently from "no" — one is an outage, the other is
    /// authorization.
    /// </returns>
    Task<bool?> IsMemberAsync(Guid chatId, string accessToken, CancellationToken cancellationToken = default);
}
