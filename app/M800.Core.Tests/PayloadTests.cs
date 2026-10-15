using M800.Core.Protocol;

namespace M800.Core.Tests;

public class PayloadTests
{
    private static string Hex(byte[] b) => Convert.ToHexString(b);

    [Theory]
    [InlineData(125, 0x08)]
    [InlineData(250, 0x04)]
    [InlineData(500, 0x02)]
    [InlineData(1000, 0x01)]
    public void Polling_codes_match_driver(int hz, byte code) => Assert.Equal(new[] { code }, Payloads.PollingRate(hz));

    [Fact]
    public void Polling_rejects_unknown_rate() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Payloads.PollingRate(2000));

    [Fact]
    public void Simple_settings()
    {
        Assert.Equal(new byte[] { 0x02 }, Payloads.LiftOff(2));
        Assert.Equal(new byte[] { 0x0C }, Payloads.Debounce(12));
        Assert.Equal(new byte[] { 0x06, 0x01 }, Payloads.Power(6, true));
        Assert.Equal(new byte[] { 0x00 }, Payloads.MotionWakeup(false));
        Assert.Equal(new byte[] { 0xFF }, Payloads.FactoryReset());
        Assert.Throws<ArgumentOutOfRangeException>(() => Payloads.LiftOff(3));
        Assert.Throws<ArgumentOutOfRangeException>(() => Payloads.Debounce(7));
        Assert.Throws<ArgumentOutOfRangeException>(() => Payloads.Debounce(32));
    }

    [Fact]
    public void Paw3370_registers()
    {
        var t = DpiTables.Get(0x70);
        Assert.Equal("PAW3370", t.Name);
        Assert.Equal(0, t.ToRegister(50));
        Assert.Equal(199, t.ToRegister(10000));
        Assert.Equal(200, t.ToRegister(10100));
        Assert.Equal(289, t.ToRegister(19000));
        Assert.Equal(50, t.Min);
        Assert.Equal(19000, t.Max);
        Assert.Throws<ArgumentOutOfRangeException>(() => t.ToRegister(10050));
    }

    [Fact]
    public void Nearest_snaps_typed_values()
    {
        var t = DpiTables.Get(0x70);
        Assert.Equal(1600, t.Nearest(1610));
        Assert.Equal(10100, t.Nearest(10090));
        Assert.Equal(50, t.Nearest(-5));
        Assert.Equal(19000, t.Nearest(99999));
    }

    [Fact]
    public void Unknown_sensor_uses_paw3370_and_paw3325_has_offset()
    {
        Assert.Equal("PAW3370", DpiTables.Get(0x00).Name);
        Assert.Equal(1, DpiTables.Get(0x25).ToRegister(200));
    }

    [Fact]
    public void Dpi_payload_layout()
    {
        var data = Payloads.Dpi([15, 31, 63, 199, 289], currentLevel: 2, enabledMask: 0x1F);
        Assert.Equal(18, data.Length);
        Assert.Equal(0x35, data[0]);   // (2+1)<<4 | 5 níveis
        Assert.Equal(0x1F, data[1]);
        Assert.Equal("0F001F003F00C7002101", Hex(data[2..12]));
        Assert.All(data[12..], b => Assert.Equal(0, b));
    }

    [Fact]
    public void Dpi_colors_fill_eight_slots()
    {
        var data = Payloads.DpiColors([new Rgb(255, 0, 0), new Rgb(0, 255, 0)]);
        Assert.Equal(24, data.Length);
        Assert.Equal("FF000000FF00", Hex(data[..6]));
        Assert.All(data[6..], b => Assert.Equal(0, b));
    }
}
