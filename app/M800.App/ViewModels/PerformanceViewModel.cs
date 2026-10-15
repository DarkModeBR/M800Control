using CommunityToolkit.Mvvm.ComponentModel;
using M800.Core.Model;
using M800.Core.Protocol;

namespace M800.App.ViewModels;

public sealed partial class PerformanceViewModel : ObservableObject
{
    private readonly AppHost _host;
    private bool _loading;

    [ObservableProperty] private int _selectedPolling;
    [ObservableProperty] private bool _lod1;
    [ObservableProperty] private bool _lod2;
    [ObservableProperty] private int _debounceMs;

    public PerformanceViewModel(AppHost host)
    {
        _host = host;
        host.ActiveProfileChanged += Load;
        host.ProfileEdited += () => { if (!_loading && SelectedPolling != _host.Active.PollingRateHz) Load(); };
        Load();
    }

    public IReadOnlyList<int> PollingRates { get; } = Payloads.PollingRates;

    public string DebounceText => $"{DebounceMs} ms";

    public void Load()
    {
        _loading = true;
        var p = _host.Active;
        SelectedPolling = p.PollingRateHz;
        Lod1 = p.LiftOffMm == 1;
        Lod2 = p.LiftOffMm == 2;
        DebounceMs = p.DebounceMs;
        _loading = false;
    }

    partial void OnSelectedPollingChanged(int value)
    {
        if (!_loading && Payloads.PollingRates.Contains(value))
            _host.Update(Section.Polling, p => p.PollingRateHz = value);
    }

    partial void OnLod1Changed(bool value)
    {
        if (!_loading && value)
            _host.Update(Section.LiftOff, p => p.LiftOffMm = 1);
    }

    partial void OnLod2Changed(bool value)
    {
        if (!_loading && value)
            _host.Update(Section.LiftOff, p => p.LiftOffMm = 2);
    }

    partial void OnDebounceMsChanged(int value)
    {
        OnPropertyChanged(nameof(DebounceText));
        if (!_loading)
            _host.Update(Section.Debounce, p => p.DebounceMs = value - value % 2);
    }
}
