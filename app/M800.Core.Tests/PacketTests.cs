using M800.Core.Protocol;

namespace M800.Core.Tests;

public class PacketTests
{
    // respostas reais capturadas do mouse (PROTOCOLO.md §3.1)
    internal const string WiredInfoHex = "0C012000010110004D3830322501007015FF6401FF01000000000000FF53158706";
    internal const string SleepingInfoHex = "0C012000000110004D3830322501007016FF4B00FF0000005955DB27FFF46F2D39";

    private static string Hex(ReadOnlySpan<byte> b) => Convert.ToHexString(b);

    [Fact]
    public void Build_lays_out_header_and_zero_pads()
    {
        var p = Packets.Build(Command.PollingRate, [0x02], sequence: 7);
        Assert.Equal(33, p.Length);
        Assert.Equal("0C01070007010102", Hex(p.AsSpan(0, 8)));
        Assert.All(p[8..], b => Assert.Equal(0, b));
    }

    [Fact]
    public void InfoRequest_has_zero_chunk_and_length() =>
        Assert.Equal("0C01200003000000", Hex(Packets.InfoRequest(3).AsSpan(0, 8)));

    [Fact]
    public void Build_rejects_oversized_payload() =>
        Assert.Throws<ArgumentException>(() => Packets.Build(Command.Macro, new byte[27], 0));

    [Fact]
    public void ParseInfo_reads_real_wired_response()
    {
        var info = Responses.ParseInfo(Convert.FromHexString(WiredInfoHex));
        Assert.NotNull(info);
        Assert.Equal("M802", info.Model);
        Assert.Equal(0x70, info.SensorId);
        Assert.Equal("PAW3370", info.SensorName);
        Assert.Equal(100, info.Battery);
        Assert.Equal(ChargeState.Charging, info.Charge);
        Assert.True(info.Online);
    }

    [Fact]
    public void ParseInfo_reads_real_sleeping_response()
    {
        var info = Responses.ParseInfo(Convert.FromHexString(SleepingInfoHex));
        Assert.NotNull(info);
        Assert.Equal(75, info.Battery);
        Assert.Equal(ChargeState.Discharging, info.Charge);
        Assert.False(info.Online);
    }

    [Fact]
    public void ParseInfo_returns_null_for_empty_response()
    {
        var r = Convert.FromHexString(WiredInfoHex);
        r[6] = 0;
        Assert.Null(Responses.ParseInfo(r));
    }

    [Fact]
    public void ParseInfo_rejects_other_commands()
    {
        var r = Convert.FromHexString(WiredInfoHex);
        r[2] = 0x05;
        Assert.Throws<FormatException>(() => Responses.ParseInfo(r));
    }

    [Fact]
    public void ParseNotification_reads_level_and_polling()
    {
        var n = Responses.ParseNotification([0x05, 0x06, 0x00, 0x02, 0x03, 0x00]);
        Assert.Equal(new DpiNotification(500, 2), n);
        Assert.Null(Responses.ParseNotification([0x01, 0x00, 0x00, 0x00, 0x00]));
    }

    [Fact]
    public void Rgb_round_trips_hex()
    {
        Assert.Equal("#60CDFF", new Rgb(0x60, 0xCD, 0xFF).ToHex());
        Assert.Equal(new Rgb(1, 2, 3), Rgb.Parse("010203"));
        Assert.False(Rgb.TryParse("xyz", out _));
    }
}
