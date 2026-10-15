using System.Text.Json;

namespace M800.Core.Model;

/// <summary>Perfis e configurações em %APPDATA%\M800Control (profiles.json, settings.json).</summary>
public sealed class ProfileStore(string dataDirectory)
{
    private sealed class ProfilesFile
    {
        public Guid ActiveProfileId { get; set; }
        public List<Section> PendingSections { get; set; } = [];
        public List<Profile> Profiles { get; set; } = [];
    }

    private string ProfilesPath => Path.Combine(DataDirectory, "profiles.json");
    private string SettingsPath => Path.Combine(DataDirectory, "settings.json");

    public string DataDirectory { get; } = dataDirectory;
    public List<Profile> Profiles { get; private set; } = [];
    public Guid ActiveProfileId { get; set; }
    public HashSet<Section> PendingSections { get; set; } = [];
    public AppSettings Settings { get; private set; } = new();

    public Profile Active => Profiles.FirstOrDefault(p => p.Id == ActiveProfileId) ?? Profiles[0];

    public void Load()
    {
        var file = ReadJson<ProfilesFile>(ProfilesPath);
        Profiles = file?.Profiles?.Where(p => p is not null).ToList() ?? [];
        if (Profiles.Count == 0)
            Profiles.Add(Profile.CreateDefault());
        foreach (var p in Profiles)
            p.Normalize();
        ActiveProfileId = file?.ActiveProfileId ?? Profiles[0].Id;
        if (Profiles.All(p => p.Id != ActiveProfileId))
            ActiveProfileId = Profiles[0].Id;
        PendingSections = file?.PendingSections?.ToHashSet() ?? [];
        Settings = ReadJson<AppSettings>(SettingsPath) ?? new AppSettings();
        Settings.BatteryAlertThreshold = Math.Clamp(Settings.BatteryAlertThreshold, 5, 50);
    }

    public void Save()
    {
        Directory.CreateDirectory(DataDirectory);
        WriteJsonAtomic(ProfilesPath, new ProfilesFile
        {
            ActiveProfileId = ActiveProfileId,
            PendingSections = PendingSections.OrderBy(s => s).ToList(),
            Profiles = Profiles,
        });
        WriteJsonAtomic(SettingsPath, Settings);
    }

    public Profile Add(Profile profile)
    {
        profile.Name = UniqueName(profile.Name);
        Profiles.Add(profile);
        return profile;
    }

    public void Delete(Guid id)
    {
        if (id == ActiveProfileId)
            throw new InvalidOperationException("Não é possível excluir o perfil ativo.");
        if (Profiles.Count <= 1)
            throw new InvalidOperationException("É preciso manter pelo menos um perfil.");
        Profiles.RemoveAll(p => p.Id == id);
    }

    /// <summary>Volta o perfil ativo aos padrões, mantendo id, nome e macros.</summary>
    public void ResetActiveToDefaults()
    {
        var current = Active;
        var fresh = Profile.CreateDefault(current.Name);
        fresh.Id = current.Id;
        fresh.Macros = current.Macros;
        Profiles[Profiles.IndexOf(current)] = fresh;
    }

    public string UniqueName(string name)
    {
        name = string.IsNullOrWhiteSpace(name) ? "Perfil" : name.Trim();
        if (Profiles.All(p => p.Name != name))
            return name;
        for (int i = 2; ; i++)
        {
            var candidate = $"{name} ({i})";
            if (Profiles.All(p => p.Name != candidate))
                return candidate;
        }
    }

    public void Export(Profile profile, string path) =>
        File.WriteAllText(path, JsonSerializer.Serialize(profile, Json.Options));

    public Profile Import(string path)
    {
        Profile? profile;
        try
        {
            profile = JsonSerializer.Deserialize<Profile>(File.ReadAllText(path), Json.Options);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("O arquivo não é um perfil válido do M800 Control.", ex);
        }
        if (profile is null)
            throw new InvalidDataException("O arquivo não é um perfil válido do M800 Control.");
        profile.Id = Guid.NewGuid();
        profile.Normalize();
        return Add(profile);
    }

    private static T? ReadJson<T>(string path) where T : class
    {
        if (!File.Exists(path))
            return null;
        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Json.Options);
        }
        catch (JsonException)
        {
            File.Copy(path, path + ".bak", overwrite: true);
            return null;
        }
    }

    private static void WriteJsonAtomic(string path, object value)
    {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(value, value.GetType(), Json.Options));
        File.Move(tmp, path, overwrite: true);
    }
}
