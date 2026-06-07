using TelegramLike.Identity.Application.Common.Interfaces;

namespace TelegramLike.Identity.Infrastructure.Auth;

internal sealed class BcryptPasswordHasher : IPasswordHasher
{
    public string Hash(string plaintext) => BCrypt.Net.BCrypt.HashPassword(plaintext);

    public bool Verify(string plaintext, string hash) => BCrypt.Net.BCrypt.Verify(plaintext, hash);
}
