using MediatR;
using TelegramLike.Application.Identity.Queries.GetUserById;

namespace TelegramLike.Application.Identity.Queries.GetUserById;

public sealed record GetUserByIdQuery(Guid UserId) : IRequest<UserDto?>;

public sealed record UserDto(
    Guid Id,
    string Email,
    string Username,
    string DisplayName,
    string? AvatarUrl,
    bool IsPremium,
    DateTime CreatedAt);
