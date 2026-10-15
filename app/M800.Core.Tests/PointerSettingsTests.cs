using M800.Core.Model;

namespace M800.Core.Tests;

public sealed class PointerSettingsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "m800ptr-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Defaults_do_not_touch_windows()
    {
        var p = Profile.CreateDefault().Pointer;
        Assert.False(p.Enabled);
        Assert.Equal(10, p.Speed);
        Assert.True(p.Acceleration);
        Assert.Equal(3, p.ScrollLines);
        Assert.Equal(500, p.DoubleClickMs);
    }

    [Fact]
    public void Normalize_clamps_values_to_windows_limits()
    {
        var p = new PointerSettings { Speed = 99, ScrollLines = -4, DoubleClickMs = 5 };
        p.Normalize();
        Assert.Equal(20, p.Speed);
        Assert.Equal(0, p.ScrollLines);   // 0 = uma tela por vez
        Assert.Equal(200, p.DoubleClickMs);
    }

    [Fact]
    public void Game_mode_turns_off_acceleration_and_uses_1_to_1_speed()
    {
        var p = new PointerSettings { Speed = 14, Acceleration = true };
        p.ApplyGameMode();
        Assert.Equal(10, p.Speed);
        Assert.False(p.Acceleration);
    }

    [Fact]
    public void Profiles_saved_before_this_feature_load_with_pointer_control_off()
    {
        Directory.CreateDirectory(_dir);
        var old = """
            { "ActiveProfileId": "11111111-1111-1111-1111-111111111111", "PendingSections": [],
              "Profiles": [ { "Id": "11111111-1111-1111-1111-111111111111", "Name": "Antigo" } ] }
            """;
        File.WriteAllText(Path.Combine(_dir, "profiles.json"), old);
        var store = new ProfileStore(_dir);
        store.Load();
        Assert.False(store.Active.Pointer.Enabled);
        Assert.Equal(10, store.Active.Pointer.Speed);
        Assert.True(store.Settings.DpiIndicatorEnabled);
    }

    [Fact]
    public void Pointer_settings_survive_save_and_load()
    {
        var store = new ProfileStore(_dir);
        store.Load();
        store.Active.Pointer.Enabled = true;
        store.Active.Pointer.Acceleration = false;
        store.Active.Pointer.ScrollLines = 0;
        store.Settings.DpiIndicatorEnabled = false;
        store.Save();

        var again = new ProfileStore(_dir);
        again.Load();
        Assert.True(again.Active.Pointer.Enabled);
        Assert.False(again.Active.Pointer.Acceleration);
        Assert.Equal(0, again.Active.Pointer.ScrollLines);
        Assert.False(again.Settings.DpiIndicatorEnabled);
    }
}
