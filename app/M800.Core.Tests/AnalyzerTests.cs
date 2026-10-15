using M800.Core.Services;

namespace M800.Core.Tests;

public class PollingAnalyzerTests
{
    private static List<double> Steady(double intervalMs, double seconds, double start = 0)
    {
        var list = new List<double>();
        for (double t = start; t < start + seconds * 1000; t += intervalMs)
            list.Add(t);
        return list;
    }

    [Fact]
    public void Steady_1ms_reports_are_1000_hz()
    {
        var r = PollingAnalyzer.Analyze(Steady(1.0, 3))!;
        Assert.InRange(r.AverageHz, 990, 1010);
        Assert.InRange(r.PeakHz, 990, 1010);
        Assert.True(r.StabilityPercent > 99);
        Assert.Equal(1000, PollingAnalyzer.NearestRate(r.AverageHz));
    }

    [Fact]
    public void Steady_8ms_reports_are_125_hz()
    {
        var r = PollingAnalyzer.Analyze(Steady(8.0, 3))!;
        Assert.InRange(r.AverageHz, 124, 126);
        Assert.Equal(125, PollingAnalyzer.NearestRate(r.AverageHz));
    }

    [Fact]
    public void Peak_is_never_below_the_average_at_low_rates()
    {
        var r = PollingAnalyzer.Analyze(Steady(15.6, 3))!; // ~64 Hz: menos de 10 amostras por janela de 100 ms
        Assert.InRange(r.AverageHz, 63, 65);
        Assert.True(r.PeakHz >= r.AverageHz);
    }

    [Fact]
    public void Pauses_while_the_mouse_is_still_are_ignored()
    {
        var samples = Steady(2.0, 1);
        samples.AddRange(Steady(2.0, 1, start: 3000));   // 2 s parado no meio
        var r = PollingAnalyzer.Analyze(samples)!;
        Assert.InRange(r.AverageHz, 490, 510);
    }

    [Fact]
    public void Irregular_reports_lower_the_stability()
    {
        var samples = new List<double>();
        double t = 0;
        for (int i = 0; i < 2000; i++)
        {
            t += i % 2 == 0 ? 0.3 : 1.7;
            samples.Add(t);
        }
        Assert.True(PollingAnalyzer.Analyze(samples)!.StabilityPercent < 50);
    }

    [Fact]
    public void Too_few_samples_give_no_result() =>
        Assert.Null(PollingAnalyzer.Analyze(Steady(1.0, 0.02)));
}

public class ClickAnalyzerTests
{
    private static List<ClickEvent> Clicks(int count, double everyMs, double pressMs, double start = 0)
    {
        var list = new List<ClickEvent>();
        for (int i = 0; i < count; i++)
        {
            double t = start + i * everyMs;
            list.Add(new ClickEvent(t, Down: true));
            list.Add(new ClickEvent(t + pressMs, Down: false));
        }
        return list;
    }

    [Fact]
    public void Normal_clicks_have_no_chatter_and_suggest_a_lower_debounce()
    {
        var r = ClickAnalyzer.Analyze(Clicks(20, 300, 70), currentDebounceMs: 12);
        Assert.Equal(20, r.Clicks);
        Assert.Equal(0, r.SuspectedChatter);
        Assert.InRange(r.AveragePressMs, 69.9, 70.1);
        Assert.InRange(r.ShortestIntervalMs, 299.9, 300.1);
        Assert.Equal(8, r.SuggestedDebounceMs);
    }

    [Fact]
    public void A_second_click_within_80ms_is_suspected_chatter_and_suggests_more_debounce()
    {
        var events = Clicks(15, 300, 60);
        events.AddRange([new ClickEvent(10_000, true), new ClickEvent(10_005, false), new ClickEvent(10_030, true), new ClickEvent(10_090, false)]);
        var r = ClickAnalyzer.Analyze(events, currentDebounceMs: 4);
        Assert.Equal(1, r.SuspectedChatter);
        Assert.Equal(8, r.SuggestedDebounceMs);
    }

    [Fact]
    public void Release_without_press_is_ignored()
    {
        var events = new List<ClickEvent> { new(0, false) };
        events.AddRange(Clicks(10, 300, 50, start: 100));
        Assert.Equal(10, ClickAnalyzer.Analyze(events, 12).Clicks);
    }

    [Fact]
    public void Few_clicks_give_no_suggestion() =>
        Assert.Null(ClickAnalyzer.Analyze(Clicks(5, 300, 50), 12).SuggestedDebounceMs);

    [Fact]
    public void Suggestions_stay_within_the_mouse_limits()
    {
        Assert.Equal(2, ClickAnalyzer.Analyze(Clicks(20, 300, 50), currentDebounceMs: 2).SuggestedDebounceMs);
        var chatter = Clicks(15, 300, 60);
        chatter.AddRange([new ClickEvent(9_000, true), new ClickEvent(9_004, false), new ClickEvent(9_020, true), new ClickEvent(9_070, false)]);
        Assert.Equal(30, ClickAnalyzer.Analyze(chatter, currentDebounceMs: 30).SuggestedDebounceMs);
    }
}
