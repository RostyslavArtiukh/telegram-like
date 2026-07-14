using Microsoft.Extensions.Configuration;
using TelegramLike.Simulator;

// 🎭 TelegramLike Simulator — «репетиція» живого трафіку: N ботів годину
// спілкуються через справжній gateway, а ти дивишся виставу у Grafana,
// RabbitMQ UI, Jaeger і Web UI. Деталі та пресети інтенсивності — у README.md.

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .AddCommandLine(args)
    .Build();

var options = configuration.GetSection("Simulator").Get<SimulatorOptions>() ?? new SimulatorOptions();
options.Validate();

var log = new SimulationLog();
log.Info("🎭 TelegramLike Simulator");
log.Info($"   ботів: {options.BotCount} · тривалість: {options.Duration:hh\\:mm\\:ss} · " +
         $"паузи: {options.MinActionDelaySeconds}–{options.MaxActionDelaySeconds}с · gateway: {options.GatewayBaseUrl}");

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    if (!cts.IsCancellationRequested)
    {
        log.Info("⏹ Ctrl+C — завершуємо виставу, актори виходять зі сцени...");
        cts.Cancel();
    }
};

var gateway = new Uri(options.GatewayBaseUrl);
var bots = new List<BotClient>();
try
{
    // 1. Каст: логін наявних акаунтів або реєстрація нових.
    log.Info("👥 Збираємо трупу...");
    foreach (var (username, displayName) in BotRoster.Cast(options.BotCount))
    {
        var bot = new BotClient(gateway, username, displayName, $"{username}@sim.telegramlike.local");
        var registered = await bot.LoginOrRegisterAsync(options.Password, cts.Token);
        bots.Add(bot);
        log.Info($"   ✓ {username} ({(registered ? "новий акаунт" : "уже знайомий")})");
    }

    // 2. Сцена: групи, канал оголошень, дірект-пари.
    log.Info("🎬 Готуємо сцену...");
    var handles = await ChatTopology.BuildAsync(bots, log, cts.Token);

    // 3. Membership-перевірки в messaging/realtime — fail-closed поверх локальних
    //    read-models, які наповнюються integration-подіями асинхронно. Даємо їм
    //    наздогнати свіжі join'и, інакше перші повідомлення зловлять 403.
    log.Info("   ⏳ 5с — чекаємо, поки membership read-models наздоженуть події...");
    await Task.Delay(TimeSpan.FromSeconds(5), cts.Token);

    // 4. Вистава.
    cts.CancelAfter(options.Duration); // відлік — від початку дії, не від реєстрацій
    log.Info("▶ Почали! Глядацькі місця:");
    log.Info("   Grafana http://localhost:3000 · RabbitMQ http://localhost:15672 · " +
             "Jaeger http://localhost:16686 · Web UI http://localhost:18080");
    log.Info($"   (у Web UI можна увійти будь-яким ботом, напр. {bots[0].Email} / пароль із конфіга)");

    var startedAt = DateTime.Now;
    var actors = bots.Select((bot, i) => new BotActor(bot, handles[i], options, log)).ToList();
    var reporter = ReportStatsLoopAsync(startedAt, cts.Token);

    await Task.WhenAll(actors.Select(a => a.RunAsync(cts.Token)));
    await reporter;
}
catch (OperationCanceledException)
{
    // Штатне завершення: Ctrl+C або вичерпана тривалість.
}
catch (Exception ex)
{
    log.Error("simulator", $"фатально: {ex.Message}");
    log.Info("   Перевір, що стек запущено: docker compose up -d --build (gateway → http://localhost:18090).");
}
finally
{
    log.Info($"🎭 Завіса. Підсумок: {log.Stats()}");
    foreach (var bot in bots)
        await bot.DisposeAsync();
}

async Task ReportStatsLoopAsync(DateTime startedAt, CancellationToken ct)
{
    while (!ct.IsCancellationRequested)
    {
        try { await Task.Delay(TimeSpan.FromSeconds(60), ct); }
        catch (OperationCanceledException) { break; }
        log.Info($"📊 {DateTime.Now - startedAt:hh\\:mm\\:ss} на сцені — {log.Stats()}");
    }
}
