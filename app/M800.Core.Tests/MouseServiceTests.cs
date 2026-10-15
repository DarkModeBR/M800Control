using M800.Core.Model;
using M800.Core.Protocol;
using M800.Core.Services;

namespace M800.Core.Tests;

public class MouseServiceTests
{
    private static readonly DateTime T0 = new(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);
    private readonly FakeFactory _factory = new();
    private readonly MouseService _svc;

    public MouseServiceTests() => _svc = new MouseService(_factory, new ListLog(), sleep: _ => { });

    private FakeTransport Mouse => _factory.Next!;

    [Fact]
    public void Tick_connects_and_reads_info()
    {
        _svc.Tick(T0);
        Assert.Equal(ConnectionState.Online, _svc.State);
        Assert.Equal(80, _svc.Info!.Battery);
        Assert.Equal(ProtocolConstants.ProductIdDongle, _svc.ProductId);
    }

    [Fact]
    public void Empty_info_response_is_retried()
    {
        var empty = FakeTransport.Info(true);
        empty[6] = 0;
        Mouse.InfoResponses.Enqueue(empty);
        _svc.Tick(T0);
        Assert.Equal(ConnectionState.Online, _svc.State);
    }

    [Fact]
    public void Submit_while_online_sends_section_and_clears_pending()
    {
        _svc.Tick(T0);
        _svc.Submit(Profile.CreateDefault(), Section.Polling);
        Assert.Contains(Section.Polling, _svc.Pending);
        _svc.Tick(T0);
        Assert.Equal(new[] { Command.PollingRate }, Mouse.Commands);
        Assert.Empty(_svc.Pending);
    }

    [Fact]
    public void Repeated_submits_of_a_section_are_coalesced()
    {
        _svc.Tick(T0);
        var p = Profile.CreateDefault();
        for (int i = 0; i < 20; i++)
        {
            p.Lighting.Brightness = 1 + i % 4;
            _svc.Submit(p, Section.Lighting);
        }
        p.Lighting.Mode = LightingMode.Wave;
        _svc.Submit(p, Section.Lighting);
        _svc.Tick(T0);
        var lighting = Assert.Single(Mouse.Sent, x => x[2] == (byte)Command.Lighting);
        Assert.Equal((byte)LightingMode.Wave, lighting[7]);
    }

    [Fact]
    public void Submit_takes_a_snapshot_of_the_profile()
    {
        _svc.Tick(T0);
        var p = Profile.CreateDefault();
        p.PollingRateHz = 1000;
        _svc.Submit(p, Section.Polling);
        p.PollingRateHz = 125;   // mudança sem novo Submit não deve vazar
        _svc.Tick(T0);
        Assert.Equal(0x01, Mouse.Sent.Single(x => x[2] == (byte)Command.PollingRate)[7]);
    }

    [Fact]
    public void Sleeping_mouse_keeps_pending_until_it_wakes()
    {
        Mouse.DefaultInfo = FakeTransport.Info(online: false);
        _svc.Tick(T0);
        Assert.Equal(ConnectionState.Sleeping, _svc.State);

        _svc.Submit(Profile.CreateDefault(), Section.Dpi);
        _svc.Tick(T0.AddSeconds(6));
        Assert.Empty(Mouse.Commands);
        Assert.Contains(Section.Dpi, _svc.Pending);

        Mouse.DefaultInfo = FakeTransport.Info(online: true);
        _svc.Tick(T0.AddSeconds(12));
        Assert.Equal(ConnectionState.Online, _svc.State);
        Assert.Equal(new[] { Command.Dpi }, Mouse.Commands);
        Assert.Empty(_svc.Pending);
    }

    [Fact]
    public void A_single_failure_is_retried()
    {
        _svc.Tick(T0);
        _svc.Submit(Profile.CreateDefault(), Section.Polling);
        Mouse.FailNextSends = 1;
        _svc.Tick(T0);
        Assert.Equal(new[] { Command.PollingRate }, Mouse.Commands);
        Assert.Empty(_svc.Pending);
    }

    [Fact]
    public void Two_failures_keep_pending_report_and_disconnect()
    {
        string? failure = null;
        _svc.SendFailed += m => failure = m;
        _svc.Tick(T0);
        _svc.Submit(Profile.CreateDefault(), Section.Polling);
        Mouse.FailNextSends = 2;
        _svc.Tick(T0);
        Assert.NotNull(failure);
        Assert.Contains(Section.Polling, _svc.Pending);
        Assert.Equal(ConnectionState.Disconnected, _svc.State);
        Assert.True(Mouse.Disposed);
    }

    [Fact]
    public void Factory_reset_clears_pending_and_sends_reset()
    {
        _svc.Tick(T0);
        _svc.Submit(Profile.CreateDefault(), Section.Polling);
        _svc.FactoryReset();
        Assert.Empty(_svc.Pending);
        _svc.Tick(T0);
        Assert.Equal(new[] { Command.FactoryReset }, Mouse.Commands);
    }

    [Fact]
    public void Missing_device_is_retried_every_5_seconds_or_when_poked()
    {
        _factory.Next = null;
        _svc.Tick(T0);
        Assert.Equal(ConnectionState.Disconnected, _svc.State);
        _svc.Tick(T0.AddSeconds(1));
        Assert.Equal(1, _factory.Opens);
        _svc.Tick(T0.AddSeconds(6));
        Assert.Equal(2, _factory.Opens);
        _svc.Poke();
        _svc.Tick(T0.AddSeconds(7));
        Assert.Equal(3, _factory.Opens);
    }

    [Fact]
    public void Flash_blinks_the_color_and_then_restores_the_profile_lighting()
    {
        _svc.Tick(T0);
        _svc.Submit(Profile.CreateDefault(), Section.Dpi);   // efeito do perfil: arco-íris em movimento
        _svc.Flash(new Rgb(0, 255, 255), T0);
        _svc.Tick(T0);

        Assert.Equal(new[] { Command.Dpi, Command.Lighting, Command.Lighting, Command.Lighting, Command.Lighting },
            Mouse.Commands);
        var lighting = Mouse.Sent.Where(x => x[2] == (byte)Command.Lighting).ToList();
        Assert.Equal("034100FFFF", Convert.ToHexString(lighting[0].AsSpan(7, 5)));   // cor fixa ciano
        Assert.Equal((byte)LightingMode.Off, lighting[1][7]);
        Assert.Equal((byte)LightingMode.Steady, lighting[2][7]);
        Assert.Equal((byte)LightingMode.ColorfulStreaming, lighting[3][7]);          // volta ao efeito do perfil
    }

    [Fact]
    public void Repeated_flashes_are_coalesced_to_the_last_color()
    {
        _svc.Tick(T0);
        _svc.Submit(Profile.CreateDefault(), Section.Dpi);
        _svc.Flash(new Rgb(255, 0, 0), T0);
        _svc.Flash(new Rgb(0, 0, 255), T0);
        _svc.Tick(T0);

        var lighting = Mouse.Sent.Where(x => x[2] == (byte)Command.Lighting).ToList();
        Assert.Equal(4, lighting.Count);
        Assert.Equal("0000FF", Convert.ToHexString(lighting[0].AsSpan(9, 3)));
    }

    [Fact]
    public void Stale_flash_is_dropped_when_the_mouse_wakes_up_later()
    {
        Mouse.DefaultInfo = FakeTransport.Info(online: false);
        _svc.Tick(T0);
        _svc.Submit(Profile.CreateDefault(), Section.Dpi);
        _svc.Flash(new Rgb(0, 255, 0), T0);

        Mouse.DefaultInfo = FakeTransport.Info(online: true);
        _svc.Tick(T0.AddSeconds(30));
        Assert.Equal(new[] { Command.Dpi }, Mouse.Commands);
    }

    [Fact]
    public void SubmitAll_sends_every_section_with_progress()
    {
        var progress = new List<(int, int)>();
        _svc.Progress += (a, b) => progress.Add((a, b));
        _svc.Tick(T0);
        _svc.SubmitAll(Profile.CreateDefault());
        _svc.Tick(T0);
        Assert.Equal(14, Mouse.Commands.Count());   // 8 seções simples + 6 botões
        Assert.Equal((14, 14), progress[^1]);
    }
}
