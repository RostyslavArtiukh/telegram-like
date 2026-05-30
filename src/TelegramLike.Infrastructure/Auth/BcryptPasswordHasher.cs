using TelegramLike.Application.Common.Interfaces;

namespace TelegramLike.Infrastructure.Auth;

internal sealed class BcryptPasswordHasher : IPasswordHasher
{
    public string Hash(string plaintext) => BCrypt.Net.BCrypt.HashPassword(plaintext);

    public bool Verify(string plaintext, string hash) => BCrypt.Net.BCrypt.Verify(plaintext, hash);
}
