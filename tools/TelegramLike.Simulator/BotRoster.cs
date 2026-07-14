namespace TelegramLike.Simulator;

/// <summary>
/// Трупа акторів. Перші десять мають імена; якщо попросити більше —
/// решта виходить на сцену як безіменна масовка (sim_bot11, sim_bot12, …).
/// Префікс <c>sim_</c> страхує від зіткнення зі справжніми користувачами.
/// </summary>
public static class BotRoster
{
    private static readonly (string Username, string DisplayName)[] NamedCast =
    [
        ("sim_olena", "Олена Пташка"),
        ("sim_taras", "Тарас Бульбашка"),
        ("sim_marichka", "Марічка Зіронька"),
        ("sim_ostap", "Остап Швидкий"),
        ("sim_solomiia", "Соломія Соловейко"),
        ("sim_bohdan", "Богдан Мудрий"),
        ("sim_oksana", "Оксана Веселка"),
        ("sim_yarema", "Ярема Нічний"),
        ("sim_daryna", "Дарина Хмаринка"),
        ("sim_lev", "Лев Хоробрий"),
    ];

    public static IEnumerable<(string Username, string DisplayName)> Cast(int count)
    {
        for (var i = 0; i < count; i++)
            yield return i < NamedCast.Length
                ? NamedCast[i]
                : ($"sim_bot{i + 1:00}", $"Сім-бот №{i + 1}");
    }
}
