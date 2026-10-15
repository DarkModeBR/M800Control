using System.Windows.Threading;
using M800.App.Platform;
using M800.Core.Hid;
using M800.Core.Model;
using M800.Core.Protocol;
using M800.Core.Services;

namespace M800.App;

/// <summary>Composição do app: perfis + serviço do mouse. Eventos são entregues na thread da interface.</summary>
public sealed class AppHost : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _saveTimer;
    private readonly BatteryAlertPolicy _batteryPolicy = new();

    private AppHost(ProfileStore store, MouseService mouse, string logPath, Dispatcher dispatcher)
    {
        Store = store;
        Mouse = mouse;
        LogPath = logPath;
        _dispatcher = dispatcher;
        _saveTimer = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = TimeSpan.FromMilliseconds(500) };
        _saveTimer.Tick += (_, _) => SaveNow();
        mouse.StatusChanged += () => Post(OnMouseStatus);
        mouse.DpiNotified += n => Post(() => OnDpiNotified(n));
        mouse.SendFailed += message => Post(() => SendFailed?.Invoke(message));
        mouse.Progress += (sent, total) => Post(() => Progress?.Invoke(sent, total));
    }

    public ProfileStore Store { get; }
    public MouseService Mouse { get; }
    public string LogPath { get; }
    public Profile Active => Store.Active;
    public AppSettings Settings => Store.Settings;
    public SensorDpiTable DpiTable => DpiTables.Get(Mouse.Info?.SensorId ?? DpiTables.Paw3370);

    public event Action? StatusChanged;
    public event Action? ActiveProfileChanged;
    public event Action? ProfilesChanged;
    public event Action? ProfileEdited;
    public event Action<int, int>? Progress;
    public event Action<string>? SendFailed;
    public event Action<DeviceInfo>? BatteryLow;

    /// <summary>O nível de DPI ativo mudou (pelo app ou pelo botão do mouse).</summary>
    public event Action? ActiveDpiChanged;

    public static AppHost Create(string dataDirectory, Dispatcher dispatcher)
    {
        Directory.CreateDirectory(dataDirectory);
        var store = new ProfileStore(dataDirectory);
        store.Load();
        var logPath = Path.Combine(dataDirectory, "log.txt");
        var mouse = new MouseService(new HidMouseTransportFactory(), new FilePacketLog(logPath));
        return new AppHost(store, mouse, logPath, dispatcher);
    }

    public void Start()
    {
        if (Store.PendingSections.Count > 0)
            Mouse.Submit(Active, Store.PendingSections.ToArray());
        Mouse.Start();
        ApplyPointer();
    }

    /// <summary>Aplica as configurações do ponteiro do Windows do perfil ativo (se o perfil controla o ponteiro).</summary>
    public void ApplyPointer()
    {
        if (Active.Pointer.Enabled)
            WindowsPointer.Apply(Active.Pointer);
    }

    /// <summary>Altera o perfil ativo e envia a seção ao mouse.</summary>
    public void Update(Section section, Action<Profile> change)
    {
        change(Active);
        ScheduleSave();
        Mouse.Submit(Active, section);
        ProfileEdited?.Invoke();
    }

    /// <summary>Troca o nível de DPI ativo e pisca a cor dele no mouse, como faz o botão de DPI.</summary>
    public void SelectDpiLevel(int index)
    {
        Update(Section.Dpi, p => p.ActiveDpiLevel = index);
        var level = Active.DpiLevels[ProfileEncoder.ActiveLevel(Active)];
        Mouse.Flash(level.Color);
        ActiveDpiChanged?.Invoke();
    }

    /// <summary>Altera o perfil ativo só no PC (ex.: o próprio mouse avisou a mudança).</summary>
    public void UpdateLocal(Action<Profile> change)
    {
        change(Active);
        ScheduleSave();
        ProfileEdited?.Invoke();
    }

    public void ActivateProfile(Guid id)
    {
        if (id == Store.ActiveProfileId)
            return;
        Store.ActiveProfileId = id;
        Mouse.SubmitAll(Active);
        ApplyPointer();
        SaveNow();
        ActiveProfileChanged?.Invoke();
        ProfilesChanged?.Invoke();
    }

    public void ResendActive() => Mouse.SubmitAll(Active);

    public void FactoryReset()
    {
        Mouse.FactoryReset();
        Store.ResetActiveToDefaults();
        SaveNow();
        ActiveProfileChanged?.Invoke();
    }

    public void ProfilesEdited()
    {
        SaveNow();
        ProfilesChanged?.Invoke();
    }

    public void SaveNow()
    {
        _saveTimer.Stop();
        Store.PendingSections = Mouse.Pending.ToHashSet();
        try
        {
            Store.Save();
        }
        catch (IOException ex)
        {
            SendFailed?.Invoke("Não foi possível salvar os perfis: " + ex.Message);
        }
    }

    private void ScheduleSave()
    {
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void Post(Action action) => _dispatcher.BeginInvoke(action);

    private void OnMouseStatus()
    {
        if (!Mouse.Pending.ToHashSet().SetEquals(Store.PendingSections))
            ScheduleSave();
        if (Mouse.Info is { Online: true } info
            && _batteryPolicy.ShouldAlert(info, Settings.BatteryAlertEnabled, Settings.BatteryAlertThreshold))
            BatteryLow?.Invoke(info);
        StatusChanged?.Invoke();
    }

    private void OnDpiNotified(DpiNotification n)
    {
        int before = Active.ActiveDpiLevel;
        UpdateLocal(p =>
        {
            if (n.DpiLevel >= 0 && n.DpiLevel < p.DpiLevels.Count)
                p.ActiveDpiLevel = n.DpiLevel;
            if (n.PollingRateHz > 0)
                p.PollingRateHz = n.PollingRateHz;
        });
        StatusChanged?.Invoke();
        if (Active.ActiveDpiLevel != before)
            ActiveDpiChanged?.Invoke();
    }

    public void Dispose()
    {
        SaveNow();
        Mouse.Dispose();
    }
}
