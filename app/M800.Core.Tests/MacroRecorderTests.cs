using M800.Core.Protocol;
using M800.Core.Services;

namespace M800.Core.Tests;

public class MacroRecorderTests
{
    private const MacroEventKind K = MacroEventKind.Key;

    [Fact]
    public void Real_delays_are_the_time_until_the_next_event()
    {
        var r = new MacroRecorder();
        r.Add(K, 0x04, false, 1000);
        r.Add(K, 0x04, true, 1050);
        r.Add(K, 0x05, false, 1080);
        r.Add(K, 0x05, true, 1200);
        Assert.Equal(new[] { 50, 30, 120, 0 }, r.Build(true, 0).Select(e => e.DelayMs));
    }

    [Fact]
    public void Fixed_delay_option()
    {
        var r = new MacroRecorder();
        r.Add(K, 0x04, false, 0);
        r.Add(K, 0x04, true, 999);
        Assert.All(r.Build(false, 20), e => Assert.Equal(20, e.DelayMs));
    }

    [Fact]
    public void Keyboard_auto_repeat_is_ignored()
    {
        var r = new MacroRecorder();
        Assert.True(r.Add(K, 0x04, false, 0));
        Assert.False(r.Add(K, 0x04, false, 30));
        Assert.False(r.Add(K, 0x04, false, 60));
        Assert.True(r.Add(K, 0x04, true, 90));
        Assert.Equal(2, r.Count);
    }

    [Fact]
    public void Release_without_press_is_ignored() => Assert.False(new MacroRecorder().Add(K, 0x04, true, 0));

    [Fact]
    public void Keys_still_held_are_released_at_the_end()
    {
        var r = new MacroRecorder();
        r.Add(MacroEventKind.Modifier, 0x01, false, 0);
        r.Add(K, 0x06, false, 10);
        r.Add(K, 0x06, true, 20);
        var events = r.Build(true, 0);
        Assert.Equal(4, events.Count);
        Assert.Equal(new MacroEvent(MacroEventKind.Modifier, 0x01, true, 0), events[^1]);
    }

    [Fact]
    public void Long_pauses_are_capped()
    {
        var r = new MacroRecorder();
        r.Add(K, 0x04, false, 0);
        r.Add(K, 0x04, true, 60_000);
        Assert.Equal(MacroEvent.MaxDelayMs, r.Build(true, 0)[0].DelayMs);
    }

    [Fact]
    public void Never_exceeds_the_event_limit()
    {
        var r = new MacroRecorder();
        for (int i = 0; i < 400; i++)
        {
            r.Add(K, (byte)(0x04 + i % 20), false, i * 10);
            r.Add(K, (byte)(0x04 + i % 20), true, i * 10 + 5);
        }
        Assert.True(r.IsFull);
        Assert.True(r.Build(true, 0).Count <= Payloads.MaxMacroEvents);
    }
}
