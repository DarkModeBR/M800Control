using M800.Core.Model;
using M800.Core.Protocol;

namespace M800.Core.Tests;

public class ProfileTests
{
    [Fact]
    public void Default_profile_matches_spec()
    {
        var p = Profile.CreateDefault();
        Assert.Equal("Padrão", p.Name);
        Assert.Equal(new[] { 400, 800, 1600, 3200, 6400, 12800 }, p.DpiLevels.Select(l => l.Dpi));
        Assert.Equal(new[] { true, true, true, true, true, false }, p.DpiLevels.Select(l => l.Enabled));
        Assert.Equal(2, p.ActiveDpiLevel);
        Assert.Equal(500, p.PollingRateHz);
        Assert.Equal(LightingMode.ColorfulStreaming, p.Lighting.Mode);
        Assert.Equal(2, p.Lighting.Brightness);
        Assert.Equal(2, p.LiftOffMm);
        Assert.Equal(12, p.DebounceMs);
        Assert.Equal(6, p.SleepMinutes);
        Assert.False(p.LightOffWhileMoving);
        Assert.True(p.MotionWakeup);
        Assert.Equal(ButtonIds.All.Select(ButtonAction.DefaultFor), p.Buttons.Select(b => b.Action));
        Assert.True(p.HasLeftClick);
    }

    [Fact]
    public void Clone_is_deep()
    {
        var p = Profile.CreateDefault();
        var c = p.Clone();
        c.DpiLevels[0].Dpi = 50;
        c.Lighting.ColorsFor(LightingMode.Breathing)[0] = Rgb.Black;
        Assert.Equal(400, p.DpiLevels[0].Dpi);
        Assert.NotEqual(Rgb.Black, p.Lighting.ColorsFor(LightingMode.Breathing)[0]);
        Assert.Equal(p.Id, c.Id);
    }

    [Fact]
    public void Duplicate_gets_new_id_and_name()
    {
        var p = Profile.CreateDefault();
        var d = p.Duplicate("Jogos");
        Assert.NotEqual(p.Id, d.Id);
        Assert.Equal("Jogos", d.Name);
    }

    [Fact]
    public void Normalize_keeps_one_enabled_level_and_valid_active()
    {
        var p = Profile.CreateDefault();
        foreach (var l in p.DpiLevels) l.Enabled = false;
        p.ActiveDpiLevel = 4;
        p.Normalize();
        Assert.True(p.DpiLevels[0].Enabled);
        Assert.Equal(0, p.ActiveDpiLevel);

        p.DpiLevels[3].Enabled = true;
        p.ActiveDpiLevel = 5;          // nível desligado
        p.Normalize();
        Assert.Equal(0, p.ActiveDpiLevel);
    }

    [Fact]
    public void Normalize_repairs_missing_data()
    {
        var p = new Profile { DpiLevels = [new DpiLevel { Dpi = 800, Enabled = true }], Buttons = [], PollingRateHz = 3, DebounceMs = 7 };
        p.Normalize();
        Assert.Equal(ProtocolConstants.MaxDpiLevels, p.DpiLevels.Count);
        Assert.Equal(6, p.Buttons.Count);
        Assert.Equal(500, p.PollingRateHz);
        Assert.Equal(6, p.DebounceMs);
    }

    [Fact]
    public void DeleteMacro_resets_buttons_using_it()
    {
        var p = Profile.CreateDefault();
        var m = new MacroDefinition { Name = "Teste" };
        p.Macros.Add(m);
        p.Buttons[(int)ButtonId.Back] = ButtonBinding.ForMacro(m.Id, 2);
        Assert.True(p.IsMacroUsed(m.Id));
        p.DeleteMacro(m.Id);
        Assert.Empty(p.Macros);
        Assert.Null(p.Buttons[(int)ButtonId.Back].MacroId);
        Assert.Equal(ButtonAction.DefaultFor(ButtonId.Back), p.Buttons[(int)ButtonId.Back].Action);
    }

    [Fact]
    public void HasLeftClick_detects_remapped_left()
    {
        var p = Profile.CreateDefault();
        p.Buttons[0] = ButtonBinding.For(ButtonAction.DpiUp);
        Assert.False(p.HasLeftClick);
        p.Buttons[2] = ButtonBinding.For(ButtonAction.Mouse(MouseButtons.Left));
        Assert.True(p.HasLeftClick);
    }
}
