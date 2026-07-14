namespace TelegramLike.Simulator;

/// <summary>
/// Уся «інтенсивність вистави» в одному місці: скільки акторів, як довго грають
/// і як часто діють. Біндиться з appsettings.json / env / CLI
/// (наприклад <c>--Simulator:BotCount=20 --Simulator:MinActionDelaySeconds=1</c>).
/// </summary>
public sealed class SimulatorOptions
{
    public string GatewayBaseUrl { get; set; } = "http://localhost:18090";
    public int BotCount { get; set; } = 10;
    public TimeSpan Duration { get; set; } = TimeSpan.FromHours(1);

    /// <summary>Пауза між діями одного бота — рівномірно з цього діапазону.</summary>
    public double MinActionDelaySeconds { get; set; } = 4;
    public double MaxActionDelaySeconds { get; set; } = 20;

    /// <summary>
    /// Один пароль на всіх ботів. Мусить лишатись тим самим між запусками —
    /// боти вже зареєстровані в Identity, і симулятор просто логіниться.
    /// </summary>
    public string Password { get; set; } = "SimBots-2026!";

    public void Validate()
    {
        if (!Uri.TryCreate(GatewayBaseUrl, UriKind.Absolute, out _))
            throw new InvalidOperationException($"Simulator:GatewayBaseUrl не є валідним URL: '{GatewayBaseUrl}'.");
        if (BotCount < 2)
            throw new InvalidOperationException("Simulator:BotCount має бути щонайменше 2 — ботам треба з ким розмовляти.");
        if (Duration <= TimeSpan.Zero)
            throw new InvalidOperationException("Simulator:Duration має бути додатною.");
        if (MinActionDelaySeconds < 0 || MaxActionDelaySeconds < MinActionDelaySeconds)
            throw new InvalidOperationException("Simulator:MinActionDelaySeconds/MaxActionDelaySeconds — некоректний діапазон.");
        if (Password.Length < 8)
            throw new InvalidOperationException("Simulator:Password закороткий — Identity вимагає мінімум 8 символів.");
    }
}
