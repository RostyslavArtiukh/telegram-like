using MediatR;

namespace TelegramLike.Identity.Application.Queries.GetUserById;

public sealed record GetUserByIdQuery(Guid UserId) : IRequest<UserDto?>;

public sealed record UserDto(
    Guid Id,
    string Email,
    string Username,
    string DisplayName,
    string? AvatarUrl,
    bool IsPremium,
    DateTime CreatedAt);
