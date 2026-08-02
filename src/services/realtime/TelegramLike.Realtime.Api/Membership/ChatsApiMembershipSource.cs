using System.Net;
using System.Net.Http.Headers;

namespace TelegramLike.Realtime.Api.Membership;

/// <summary>
/// Answers membership by asking the Chats service for the chat, **as the connecting user**.
/// </summary>
/// <remarks>
/// <c>GET /chats/{id}</c> is already member-only and deliberately hides a chat the caller is
/// not in as a 404, so it is exactly the oracle needed here — and forwarding the user's own
/// token means this service needs no credentials of its own and can grant nothing the user
/// could not already read.
/// <para>
/// Straight to Chats rather than through the gateway: the gateway is the front door for
/// external clients, and in compose it already depends on this service, so routing back
/// through it would be a cycle. This is a backend calling a backend.
/// </para>
/// </remarks>
internal sealed class ChatsApiMembershipSource(HttpClient http, ILogger<ChatsApiMembershipSource> logger)
    : IChatMembershipSource
{
    public async Task<bool?> IsMemberAsync(
        Guid chatId, string accessToken, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/chats/{chatId}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await http.SendAsync(request, cancellationToken);

            // 404 is the "not a member (or no such chat)" answer, not an error — Chats hides
            // both behind the same status so a non-member cannot probe which chat ids exist.
            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden) return false;
            if (response.IsSuccessStatusCode) return true;

            logger.LogWarning(
                "Chats answered {Status} when asked about membership of chat {ChatId}; treating it as unknown.",
                (int)response.StatusCode,
                chatId);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not reach Chats to check membership of chat {ChatId}.", chatId);
            return null;
        }
    }
}
