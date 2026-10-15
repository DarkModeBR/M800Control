using M800.Core.Model;
using M800.Core.Protocol;
using M800.Core.Services;

namespace M800.Core.Tests;

public class ProfileEncoderTests
{
    private static readonly SensorDpiTable Paw3370 = DpiTables.Get(0x70);

    [Fact]
    public void Each_simple_section_is_one_packet_with_the_right_command()
    {
        var p = Profile.CreateDefault();
        Assert.Equal(Command.PollingRate, ProfileEncoder.Encode(p, Section.Polling, Paw3370).Single().Command);
        Assert.Equal(Command.Lighting, ProfileEncoder.Encode(p, Section.Lighting, Paw3370).Single().Command);
        Assert.Equal(Command.LiftOff, ProfileEncoder.Encode(p, Section.LiftOff, Paw3370).Single().Command);
        Assert.Equal(Command.Debounce, ProfileEncoder.Encode(p, Section.Debounce, Paw3370).Single().Command);
        Assert.Equal(Command.Power, ProfileEncoder.Encode(p, Section.Power, Paw3370).Single().Command);
        Assert.Equal(Command.MotionWakeup, ProfileEncoder.Encode(p, Section.MotionWakeup, Paw3370).Single().Command);
        Assert.Equal(Command.DpiColors, ProfileEncoder.Encode(p, Section.DpiColors, Paw3370).Single().Command);
    }

    [Fact]
    public void Default_dpi_packet()
    {
        var pkt = ProfileEncoder.Encode(Profile.CreateDefault(), Section.Dpi, Paw3370).Single();
        Assert.Equal(Command.Dpi, pkt.Command);
        Assert.Equal(0x35, pkt.Payload[0]);   // nível 3 ativo, 5 habilitados
        Assert.Equal(0x1F, pkt.Payload[1]);
        Assert.Equal("07000F001F003F007F00E300", Convert.ToHexString(pkt.Payload[2..14]));   // 12800 → reg 227
    }

    [Fact]
    public void Dpi_falls_back_to_first_enabled_level()
    {
        var p = Profile.CreateDefault();
        p.ActiveDpiLevel = 5;                 // nível 6 está desligado
        Assert.Equal(0, ProfileEncoder.ActiveLevel(p));
        var pkt = ProfileEncoder.Encode(p, Section.Dpi, Paw3370).Single();
        Assert.Equal(0x15, pkt.Payload[0]);
    }

    [Fact]
    public void Dpi_snaps_values_outside_the_table()
    {
        var p = Profile.CreateDefault();
        p.DpiLevels[0].Dpi = 10050;
        var pkt = ProfileEncoder.Encode(p, Section.Dpi, Paw3370).Single();
        Assert.Equal(199, pkt.Payload[2] | pkt.Payload[3] << 8);   // 10000 DPI
    }

    [Fact]
    public void Lighting_uses_colors_of_current_mode()
    {
        var p = Profile.CreateDefault();
        p.Lighting.Mode = LightingMode.Steady;
        p.Lighting.ColorsFor(LightingMode.Steady)[0] = new Rgb(1, 2, 3);
        var pkt = ProfileEncoder.Encode(p, Section.Lighting, Paw3370).Single();
        Assert.Equal("0322010203", Convert.ToHexString(pkt.Payload));
    }

    [Fact]
    public void Default_buttons_are_six_key_packets_in_firmware_order()
    {
        var packets = ProfileEncoder.Encode(Profile.CreateDefault(), Section.Buttons, Paw3370);
        Assert.Equal(6, packets.Count);
        Assert.All(packets, x => Assert.Equal(Command.Key, x.Command));
        Assert.Equal(new byte[] { 0, 1, 2, 3, 4, 5 }, packets.Select(x => x.Payload[0]));
        Assert.Equal("03100800", Convert.ToHexString(packets[3].Payload));   // voltar
    }

    [Fact]
    public void Macros_are_sent_before_keys_with_slots_in_button_order()
    {
        var p = Profile.CreateDefault();
        var a = new MacroDefinition { Name = "a", Events = [new MacroEvent(MacroEventKind.Key, 4, false, 10), new MacroEvent(MacroEventKind.Key, 4, true, 10)] };
        var b = new MacroDefinition { Name = "b" };
        p.Macros.AddRange([a, b]);
        p.Buttons[(int)ButtonId.Forward] = ButtonBinding.ForMacro(b.Id, 1);
        p.Buttons[(int)ButtonId.Back] = ButtonBinding.ForMacro(a.Id, 3);

        var packets = ProfileEncoder.Encode(p, Section.Buttons, Paw3370);
        Assert.Equal(new[] { Command.Macro, Command.Macro, Command.Key, Command.Key, Command.Key, Command.Key, Command.Key, Command.Key },
            packets.Select(x => x.Command));
        Assert.Equal(1, packets[0].Payload[0]);                                          // "a" (voltar, índice 3) → slot 1
        Assert.Equal(2, packets[1].Payload[0]);                                          // "b" (avançar, índice 4) → slot 2
        Assert.Equal("03902103", Convert.ToHexString(packets[2 + 3].Payload));            // voltar → macro slot 1, 3×
        Assert.Equal("04902201", Convert.ToHexString(packets[2 + 4].Payload));            // avançar → macro slot 2, 1×
    }

    [Fact]
    public void Same_macro_on_two_buttons_is_sent_once()
    {
        var p = Profile.CreateDefault();
        var m = new MacroDefinition { Name = "m" };
        p.Macros.Add(m);
        p.Buttons[3] = ButtonBinding.ForMacro(m.Id, 1);
        p.Buttons[4] = ButtonBinding.ForMacro(m.Id, 1);
        Assert.Single(ProfileEncoder.Encode(p, Section.Buttons, Paw3370), x => x.Command == Command.Macro);
    }

    [Fact]
    public void Button_with_missing_macro_falls_back_to_default()
    {
        var p = Profile.CreateDefault();
        p.Buttons[3] = ButtonBinding.ForMacro(Guid.NewGuid(), 1);
        var packets = ProfileEncoder.Encode(p, Section.Buttons, Paw3370);
        Assert.Equal("03100800", Convert.ToHexString(packets[3].Payload));
    }

    [Fact]
    public void EncodeAll_covers_every_section()
    {
        var commands = ProfileEncoder.EncodeAll(Profile.CreateDefault(), Paw3370).Select(x => x.Command).Distinct().ToHashSet();
        Assert.Equal(new[] { Command.Key, Command.Dpi, Command.DpiColors, Command.PollingRate, Command.Lighting,
            Command.LiftOff, Command.Debounce, Command.Power, Command.MotionWakeup }.ToHashSet(), commands);
    }
}
