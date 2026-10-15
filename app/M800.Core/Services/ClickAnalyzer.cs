namespace M800.Core.Services;

public sealed record ClickEvent(double TimeMs, bool Down);

/// <param name="SuggestedDebounceMs">null quando há poucos cliques para concluir algo.</param>
public sealed record ClickResult(int Clicks, double AveragePressMs, double ShortestIntervalMs, int SuspectedChatter, int? SuggestedDebounceMs);

/// <summary>Analisa um teste de cliques: duração do aperto, intervalos e possível repique.</summary>
public static class ClickAnalyzer
{
    /// <summary>Uma pessoa dificilmente clica duas vezes em menos que isso (início a início).</summary>
    public const double ChatterIntervalMs = 80;

    public const int MinClicks = 10;

    public static ClickResult Analyze(IReadOnlyList<ClickEvent> events, int currentDebounceMs)
    {
        var downs = new List<double>();
        var presses = new List<double>();
        double? pressedAt = null;
        foreach (var e in events.OrderBy(e => e.TimeMs))
        {
            if (e.Down)
            {
                if (pressedAt is null)
                {
                    pressedAt = e.TimeMs;
                    downs.Add(e.TimeMs);
                }
            }
            else if (pressedAt is double start)
            {
                presses.Add(e.TimeMs - start);
                pressedAt = null; // soltar sem apertar antes (ex.: o clique em "Iniciar") é ignorado
            }
        }

        var intervals = downs.Zip(downs.Skip(1), (a, b) => b - a).ToList();
        int chatter = intervals.Count(i => i < ChatterIntervalMs);
        int? suggestion = downs.Count < MinClicks
            ? null
            : chatter > 0
                ? Math.Min(30, currentDebounceMs + 4)
                : Math.Max(2, currentDebounceMs - 4);

        return new ClickResult(
            downs.Count,
            presses.Count > 0 ? presses.Average() : 0,
            intervals.Count > 0 ? intervals.Min() : 0,
            chatter,
            suggestion);
    }
}
