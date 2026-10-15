using M800.Core.Model;
using M800.Core.Protocol;

namespace M800.Core.Tests;

public sealed class ProfileStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "m800tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void First_load_creates_default_profile()
    {
        var s = new ProfileStore(_dir);
        s.Load();
        Assert.Single(s.Profiles);
        Assert.Equal("Padrão", s.Active.Name);
        Assert.False(s.Settings.FirstRunDone);
    }

    [Fact]
    public void Save_and_load_round_trip()
    {
        var s = new ProfileStore(_dir);
        s.Load();
        var games = s.Add(s.Active.Duplicate("Jogos"));
        games.Lighting.Mode = LightingMode.Breathing;
        games.Macros.Add(new MacroDefinition { Name = "m", Events = [new MacroEvent(MacroEventKind.Key, 4, false, 20)] });
        s.ActiveProfileId = games.Id;
        s.PendingSections = [Section.Lighting, Section.Dpi];
        s.Settings.BatteryAlertThreshold = 30;
        s.Save();

        var again = new ProfileStore(_dir);
        again.Load();
        Assert.Equal(2, again.Profiles.Count);
        Assert.Equal("Jogos", again.Active.Name);
        Assert.Equal(LightingMode.Breathing, again.Active.Lighting.Mode);
        Assert.Equal(new MacroEvent(MacroEventKind.Key, 4, false, 20), again.Active.Macros[0].Events[0]);
        Assert.Equal(new[] { Section.Dpi, Section.Lighting }, again.PendingSections.OrderBy(x => x));
        Assert.Equal(30, again.Settings.BatteryAlertThreshold);
    }

    [Fact]
    public void Corrupt_profiles_file_falls_back_to_default_and_keeps_backup()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "profiles.json"), "{ isto não é json");
        var s = new ProfileStore(_dir);
        s.Load();
        Assert.Single(s.Profiles);
        Assert.True(File.Exists(Path.Combine(_dir, "profiles.json.bak")));
    }

    [Fact]
    public void Import_gives_new_id_and_unique_name()
    {
        var s = new ProfileStore(_dir);
        s.Load();
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".m800profile");
        s.Export(s.Active, path);
        var imported = s.Import(path);
        File.Delete(path);
        Assert.NotEqual(s.Profiles[0].Id, imported.Id);
        Assert.Equal("Padrão (2)", imported.Name);
    }

    [Fact]
    public void Import_rejects_invalid_file()
    {
        var s = new ProfileStore(_dir);
        s.Load();
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".m800profile");
        File.WriteAllText(path, "abc");
        Assert.Throws<InvalidDataException>(() => s.Import(path));
        File.Delete(path);
    }

    [Fact]
    public void Cannot_delete_active_or_last_profile()
    {
        var s = new ProfileStore(_dir);
        s.Load();
        Assert.Throws<InvalidOperationException>(() => s.Delete(s.ActiveProfileId));
        var other = s.Add(Profile.CreateDefault("Outro"));
        s.Delete(other.Id);
        Assert.Single(s.Profiles);
    }

    [Fact]
    public void Reset_keeps_identity_and_macros()
    {
        var s = new ProfileStore(_dir);
        s.Load();
        var id = s.Active.Id;
        s.Active.Name = "Meu";
        s.Active.PollingRateHz = 1000;
        s.Active.Macros.Add(new MacroDefinition { Name = "m" });
        s.ResetActiveToDefaults();
        Assert.Equal(id, s.Active.Id);
        Assert.Equal("Meu", s.Active.Name);
        Assert.Equal(500, s.Active.PollingRateHz);
        Assert.Single(s.Active.Macros);
    }
}
