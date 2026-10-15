using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using M800.Core.Model;
using M800.Core.Protocol;
using M800.Core.Services;

namespace M800.App.ViewModels;

public sealed partial class HomeViewModel : ObservableObject
{
    private readonly AppHost _host;

    [ObservableProperty] private string _connectionText = "";
    [ObservableProperty] private string _batteryText = "—";
    [ObservableProperty] private string _batteryDetail = "";
    [ObservableProperty] private double _batteryFillWidth;
    [ObservableProperty] private string _dpiText = "—";
    [ObservableProperty] private string _dpiDetail = "";
    [ObservableProperty] private string _pollingText = "—";
    [ObservableProperty] private string _profileText = "";
    [ObservableProperty] private string _modelText = "—";
    [ObservableProperty] private string _sensorText = "—";
    [ObservableProperty] private string _rangeText = "";

    public HomeViewModel(AppHost host)
    {
        _host = host;
        host.StatusChanged += Refresh;
        host.ProfileEdited += Refresh;
        host.ActiveProfileChanged += Refresh;
        host.ProfilesChanged += Refresh;
        Refresh();
    }

    public ObservableCollection<ChipItem> DpiChips { get; } = [];
    public ObservableCollection<ChipItem> ProfileChips { get; } = [];

    public void Refresh()
    {
        var profile = _host.Active;
        var info = _host.Mouse.Info;
        ConnectionText = _host.Mouse.State switch
        {
            ConnectionState.Online when _host.Mouse.ProductId == ProtocolConstants.ProductIdWired => "Conectado pelo cabo USB",
            ConnectionState.Online => "Conectado pelo receptor 2.4 GHz",
            ConnectionState.Sleeping => "Receptor conectado · mouse dormindo",
            _ => "Mouse não encontrado",
        };

        if (info is null)
        {
            BatteryText = "—";
            BatteryDetail = "Sem informação";
            BatteryFillWidth = 0;
        }
        else
        {
            BatteryText = $"{info.Battery}%";
            BatteryDetail = info.Charge switch
            {
                ChargeState.Charging => "Carregando",
                ChargeState.Full => "Carga completa",
                _ => "Descarregando",
            };
            BatteryFillWidth = 34 * info.Battery / 100.0;
        }

        int active = ProfileEncoder.ActiveLevel(profile);
        var enabled = profile.DpiLevels.Select((level, index) => (level, index)).Where(x => x.level.Enabled).ToList();
        DpiText = profile.DpiLevels[active].Dpi.ToString("N0");
        DpiDetail = $"Nível {enabled.FindIndex(x => x.index == active) + 1} de {enabled.Count}";
        PollingText = $"{profile.PollingRateHz} Hz";
        ProfileText = profile.Name;
        ModelText = info is null ? "—" : $"M800 PRO ({info.Model})";
        SensorText = info?.SensorName ?? "—";
        var table = _host.DpiTable;
        RangeText = $"{table.Min:N0} – {table.Max:N0} DPI";

        DpiChips.Clear();
        foreach (var (level, index) in enabled)
        {
            int target = index;
            DpiChips.Add(new ChipItem(level.Dpi.ToString("N0"), $"Nível {index + 1}", index == active,
                new RelayCommand(() => _host.SelectDpiLevel(target))));
        }

        ProfileChips.Clear();
        foreach (var p in _host.Store.Profiles)
        {
            var id = p.Id;
            ProfileChips.Add(new ChipItem(p.Name, "", id == profile.Id, new RelayCommand(() => _host.ActivateProfile(id))));
        }
    }
}
