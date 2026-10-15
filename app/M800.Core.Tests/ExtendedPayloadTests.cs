using M800.Core.Protocol;

namespace M800.Core.Tests;

public class ExtendedPayloadTests
{
    private static string Hex(byte[] b) => Convert.ToHexString(b);

    [Fact]
    public void Steady_light_has_one_color()
    {
        var data = Payloads.Lighting(LightingMode.Steady, 4, 2, [new Rgb(1, 2, 3)], 0);
        Assert.Equal("0342010203", Hex(data));
    }

    [Fact]
    public void Breathing_skips_black_and_counts_colors()
    {
        var data = Payloads.Lighting(LightingMode.Breathing, 1, 1,
            [new Rgb(9, 9, 9), Rgb.Black, new Rgb(7, 7, 7)], 0);
        Assert.Equal("021102090909070707", Hex(data));
    }

    [Fact]
    public void Off_matches_tested_packet() =>
        Assert.Equal("0042", Hex(Payloads.Lighting(LightingMode.Off, 4, 2, [], 0)));

    [Fact]
    public void Streaming_carries_direction() =>
        Assert.Equal("012301", Hex(Payloads.Lighting(LightingMode.ColorfulStreaming, 2, 3, [], 1)));

    [Fact]
    public void Long_modes_have_driver_lengths()
    {
        Assert.Equal(23, Payloads.Lighting(LightingMode.ColorfulSteady, 1, 1, [], 0).Length);
        var flicker = Payloads.Lighting(LightingMode.Flicker, 1, 1, [new Rgb(1, 2, 3), new Rgb(4, 5, 6)], 0);
        Assert.Equal(23, flicker.Length);
        Assert.Equal("0711" + "000203040506", Hex(flicker[..8])); // driver zera o 1º byte (mantido igual)
    }

    [Fact]
    public void Button_actions_match_driver_tables()
    {
        Assert.Equal(new ButtonAction(0x10, 0x01, 0x00), ButtonAction.DefaultFor(ButtonId.Left));
        Assert.Equal(new ButtonAction(0x10, 0x08, 0x00), ButtonAction.DefaultFor(ButtonId.Back));
        Assert.Equal(new ButtonAction(0x10, 0x10, 0x00), ButtonAction.DefaultFor(ButtonId.Forward));
        Assert.Equal(new ButtonAction(0x40, 0x01, 0x00), ButtonAction.DefaultFor(ButtonId.Dpi));
        Assert.Equal(new ButtonAction(0x80, 0x92, 0x01), ButtonAction.Consumer(ConsumerKeys.Calculator));
        Assert.Equal(new ButtonAction(0x70, 0x04, 0x3D), ButtonAction.Key(0x3D, Modifiers.LAlt));
        Assert.Equal(new ButtonAction(0x30, 0x32, 0x03), ButtonAction.TripleClick);
        Assert.Equal(new ButtonAction(0x60, 0x04, 0x00), ButtonAction.SwitchLighting);
    }

    [Fact]
    public void Macro_action_uses_repeat_mode_2()
    {
        Assert.Equal(new ButtonAction(0x90, 0x21, 0x01), ButtonAction.Macro(1, 1));
        Assert.Equal(new ButtonAction(0x90, 0x23, 0x05), ButtonAction.Macro(3, 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => ButtonAction.Macro(16, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ButtonAction.Macro(1, 0));
    }

    [Fact]
    public void Key_payload_layout() =>
        Assert.Equal("05400200", Hex(Payloads.Key(ButtonId.Dpi, ButtonAction.DpiUp)));

    [Fact]
    public void Macro_event_encoding()
    {
        Assert.Equal("30AC04", Hex(new MacroEvent(MacroEventKind.Key, 0x04, false, 0x0AC).Encode()));
        Assert.Equal("B12304", Hex(new MacroEvent(MacroEventKind.Key, 0x04, true, 0x123).Encode()));
        Assert.Equal("2FFF01", Hex(new MacroEvent(MacroEventKind.Modifier, 0x01, false, 99999).Encode()));
    }

    [Fact]
    public void Macro_chunking()
    {
        var events = Enumerable.Range(0, 10).Select(i => new MacroEvent(MacroEventKind.Key, 0x04, i % 2 == 1, 10)).ToList();
        var chunks = Payloads.MacroChunks(2, events);
        Assert.Equal(new byte[] { 0x02, 0x12 }, chunks.Select(c => c.Chunk));
        Assert.Equal(26, chunks[0].Payload.Length);
        Assert.Equal(6, chunks[1].Payload.Length);
        Assert.Equal(2, chunks[0].Payload[0]);
        Assert.Equal(new byte[] { 0x01 }, Payloads.MacroChunks(1, []).Single().Payload);
    }

    [Fact]
    public void Macro_limits()
    {
        var tooMany = Enumerable.Repeat(new MacroEvent(MacroEventKind.Key, 4, false, 0), 126).ToList();
        Assert.Throws<ArgumentException>(() => Payloads.MacroChunks(1, tooMany));
        Assert.Throws<ArgumentOutOfRangeException>(() => Payloads.MacroChunks(0, []));
    }

    [Fact]
    public void KeyMap_translates_windows_keys()
    {
        Assert.Equal(0x04, KeyMap.VkToHid(0x41));      // A
        Assert.Equal(0x28, KeyMap.VkToHid(0x0D));      // Enter
        Assert.Equal(0x41, KeyMap.HidToVk(0x04));
        Assert.Equal((MacroEventKind.Modifier, (byte)0x01), KeyMap.MacroCodeForVk(0xA2)); // Ctrl esq.
        Assert.Equal((MacroEventKind.Modifier, (byte)0x40), KeyMap.MacroCodeForVk(0xA5)); // Alt dir.
        Assert.Equal((MacroEventKind.Key, (byte)0x87), KeyMap.MacroCodeForVk(0xC1));      // ABNT2 "/"
        Assert.Equal((MacroEventKind.Key, (byte)0x64), KeyMap.MacroCodeForVk(0xE2));      // ABNT2 "\"
        Assert.Null(KeyMap.MacroCodeForVk(0x01));                                         // botão do mouse
        Assert.Equal(new[] { "Ctrl", "Shift" }, KeyMap.ModifierNames(0x03));
    }

    [Fact]
    public void Lighting_mode_metadata()
    {
        Assert.Equal(7, LightingModes.MaxColors(LightingMode.Breathing));
        Assert.Equal(1, LightingModes.MinColors(LightingMode.Breathing));
        Assert.Equal(2, LightingModes.MinColors(LightingMode.Flicker));
        Assert.True(LightingModes.UsesDirection(LightingMode.ColorfulStreaming));
        Assert.False(LightingModes.UsesSpeedAndBrightness(LightingMode.Steady));
        Assert.Equal(10, LightingModes.All.Count);
    }
}
