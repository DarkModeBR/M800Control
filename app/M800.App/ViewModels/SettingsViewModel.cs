using System.Diagnostics;
using System.Reflection;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using M800.App.Common;
using M800.App.Platform;
using M800.Core.Model;

namespace M800.App.ViewModels;

public sealed record ThemeItem(AppTheme Theme, string Name);

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly AppHost _host;
    private bool _loading;

    [ObservableProperty] private bool _startWithWindows;
    [ObservableProperty] private bool _batteryAlertEnabled;
    [ObservableProperty] private int _batteryAlertThreshold;
    [ObservableProperty] private bool _dpiIndicatorEnabled;
    [ObservableProperty] private ThemeItem? _selectedTheme;
    [ObservableProperty] private string _aboutText = "";

    public SettingsViewModel(AppHost host)
    {
        _host = host;
        host.StatusChanged += UpdateAbout;
        Load();
    }

    public IReadOnlyList<ThemeItem> Themes { get; } =
    [
        new(AppTheme.System, "Igual ao Windows"),
        new(AppTheme.Light, "Claro"),
        new(AppTheme.Dark, "Escuro"),
    ];

    public string ThresholdText => $"{BatteryAlertThreshold}%";

    public void Load()
    {
        _loading = true;
        var s = _host.Settings;
        StartWithWindows = s.StartWithWindows;
        BatteryAlertEnabled = s.BatteryAlertEnabled;
        BatteryAlertThreshold = s.BatteryAlertThreshold;
        DpiIndicatorEnabled = s.DpiIndicatorEnabled;
        SelectedTheme = Themes.First(t => t.Theme == s.Theme);
        _loading = false;
        UpdateAbout();
    }

    private void UpdateAbout()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
        var info = _host.Mouse.Info;
        AboutText = $"M800 Control {version}\n" +
                    $"Mouse: {(info is null ? "não conectado" : $"M800 PRO ({info.Model}), sensor {info.SensorName}")}\n" +
                    $"Dados: {_host.Store.DataDirectory}\n" +
                    "Feito a partir da engenharia reversa do driver oficial (veja PROTOCOLO.md).";
    }

    partial void OnStartWithWindowsChanged(bool value)
    {
        if (_loading)
            return;
        _host.Settings.StartWithWindows = value;
        StartupRegistration.Sync(value);
        _host.SaveNow();
    }

    partial void OnBatteryAlertEnabledChanged(bool value)
    {
        if (_loading)
            return;
        _host.Settings.BatteryAlertEnabled = value;
        _host.SaveNow();
    }

    partial void OnBatteryAlertThresholdChanged(int value)
    {
        OnPropertyChanged(nameof(ThresholdText));
        if (_loading)
            return;
        _host.Settings.BatteryAlertThreshold = value;
        _host.SaveNow();
    }

    partial void OnDpiIndicatorEnabledChanged(bool value)
    {
        if (_loading)
            return;
        _host.Settings.DpiIndicatorEnabled = value;
        _host.SaveNow();
    }

    partial void OnSelectedThemeChanged(ThemeItem? value)
    {
        if (_loading || value is null)
            return;
        _host.Settings.Theme = value.Theme;
        ThemeService.Apply(value.Theme, Application.Current.MainWindow);
        _host.SaveNow();
    }

    [RelayCommand]
    private void OpenDataFolder() =>
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_host.Store.DataDirectory}\"") { UseShellExecute = true });

    [RelayCommand]
    private async Task OpenLogAsync()
    {
        if (!File.Exists(_host.LogPath))
        {
            await Dialogs.InfoAsync("Log vazio", "Nenhum pacote foi enviado ainda.");
            return;
        }
        Process.Start(new ProcessStartInfo(_host.LogPath) { UseShellExecute = true });
    }

    [RelayCommand]
    private async Task FactoryResetAsync()
    {
        if (!await Dialogs.ConfirmAsync("Restaurar padrões de fábrica",
                "O mouse volta às configurações de fábrica e o perfil ativo volta aos padrões (as macros são mantidas).",
                "Restaurar"))
            return;
        _host.FactoryReset();
        await Dialogs.InfoAsync("Pronto", "O comando de reset foi enviado ao mouse.");
    }
}
