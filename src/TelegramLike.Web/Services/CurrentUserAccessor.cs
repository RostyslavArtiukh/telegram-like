using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace TelegramLike.Web.Services;

public sealed class CurrentUserAccessor(AuthenticationStateProvider authStateProvider)
{
    public async Task<Guid?> GetUserIdAsync()
    {
        var state = await authStateProvider.GetAuthenticationStateAsync();
        var raw = state.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    public async Task<(Guid Id, string Username)?> GetUserAsync()
    {
        var state = await authStateProvider.GetAuthenticationStateAsync();
        var id = state.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var name = state.User.FindFirst(ClaimTypes.Name)?.Value;
        if (id is null || name is null || !Guid.TryParse(id, out var guid)) return null;
        return (guid, name);
    }
}
