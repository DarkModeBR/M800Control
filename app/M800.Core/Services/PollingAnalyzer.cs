namespace M800.Core.Services;

public sealed record PollingResult(int Reports, double AverageHz, double PeakHz, double StabilityPercent);

/// <summary>Calcula a taxa real de relatórios do mouse a partir dos horários de chegada (em ms).</summary>
public static class PollingAnalyzer
{
    /// <summary>Intervalos maiores que isso são pausas (mouse parado), não relatórios.</summary>
    public const double PauseMs = 50;

    public const int MinIntervals = 100;

    public static readonly int[] KnownRates = [125, 250, 500, 1000];

    public static PollingResult? Analyze(IReadOnlyList<double> timestampsMs)
    {
        var intervals = new List<double>();
        for (int i = 1; i < timestampsMs.Count; i++)
        {
            double d = timestampsMs[i] - timestampsMs[i - 1];
            if (d > 0 && d <= PauseMs)
                intervals.Add(d);
        }
        if (intervals.Count < MinIntervals)
            return null;

        double mean = intervals.Average();
        double averageHz = 1000.0 / mean;
        double stable = intervals.Count(d => Math.Abs(d - mean) <= mean * 0.25) * 100.0 / intervals.Count;
        return new PollingResult(intervals.Count, averageHz, Math.Max(averageHz, PeakHz(timestampsMs)), stable);
    }

    /// <summary>Maior taxa sustentada numa janela de até 100 ms (ignora janelas com pausa).</summary>
    private static double PeakHz(IReadOnlyList<double> t)
    {
        double best = 0;
        int start = 0;
        for (int end = 1; end < t.Count; end++)
        {
            if (t[end] - t[end - 1] > PauseMs)
            {
                start = end;
                continue;
            }
            while (t[end] - t[start] >= 100)
                start++;
            int samples = end - start + 1;
            double span = t[end] - t[start];
            if (samples >= 10 && span > 0)
                best = Math.Max(best, (samples - 1) * 1000.0 / span);
        }
        return best;
    }

    public static int NearestRate(double hz) => KnownRates.MinBy(r => Math.Abs(r - hz));
}
