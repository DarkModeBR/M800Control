using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using M800.App.Platform;
using M800.Core.Model;

namespace M800.App.ViewModels;

public sealed partial class PointerViewModel : ObservableObject
{
    private readonly AppHost _host;
    private readonly DispatcherTimer _applyTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private bool _loading;

    [ObservableProperty] private bool _enabled;
    [ObservableProperty] private int _speed;
    [ObservableProperty] private bool _acceleration;
    [ObservableProperty] private bool _pageScroll;
    [ObservableProperty] private int _scrollLines;
    [ObservableProperty] private int _doubleClickMs;

    public PointerViewModel(AppHost host)
    {
        _host = host;
        // aplica no Windows só depois que o slider para (cada aplicação avisa todas as janelas do sistema)
        _applyTimer.Tick += (_, _) =>
        {
            _applyTimer.Stop();
            _host.ApplyPointer();
        };
        host.ActiveProfileChanged += Load;
        Load();
    }

    public string SpeedText => Speed == 10 ? "10 (1:1)" : Speed.ToString();
    public string ScrollText => ScrollLines == 1 ? "1 linha" : $"{ScrollLines} linhas";
    public string DoubleClickText => $"{DoubleClickMs} ms";

    public void Load()
    {
        _loading = true;
        var p = _host.Active.Pointer;
        Enabled = p.Enabled;
        Speed = p.Speed;
        Acceleration = p.Acceleration;
        PageScroll = p.ScrollLines == 0;
        ScrollLines = p.ScrollLines == 0 ? 3 : p.ScrollLines;
        DoubleClickMs = p.DoubleClickMs;
        _loading = false;
    }

    private void Edit(Action<PointerSettings> change)
    {
        if (_loading)
            return;
        _host.UpdateLocal(p => change(p.Pointer));
        _applyTimer.Stop();
        _applyTimer.Start();
    }

    partial void OnEnabledChanged(bool value)
    {
        if (_loading)
            return;
        if (value)
        {
            // começa dos valores que o Windows usa hoje, para nada mudar de surpresa
            var current = WindowsPointer.ReadCurrent();
            current.Enabled = true;
            _host.UpdateLocal(p => p.Pointer = current);
            Load();
        }
        else
        {
            _host.UpdateLocal(p => p.Pointer.Enabled = false);
        }
    }

    partial void OnSpeedChanged(int value)
    {
        OnPropertyChanged(nameof(SpeedText));
        Edit(p => p.Speed = value);
    }

    partial void OnAccelerationChanged(bool value) => Edit(p => p.Acceleration = value);

    partial void OnPageScrollChanged(bool value) => Edit(p => p.ScrollLines = value ? 0 : ScrollLines);

    partial void OnScrollLinesChanged(int value)
    {
        OnPropertyChanged(nameof(ScrollText));
        if (!PageScroll)
            Edit(p => p.ScrollLines = value);
    }

    partial void OnDoubleClickMsChanged(int value)
    {
        OnPropertyChanged(nameof(DoubleClickText));
        Edit(p => p.DoubleClickMs = value);
    }

    [RelayCommand]
    private void GameMode()
    {
        Edit(p => p.ApplyGameMode());
        Load();
    }

    [RelayCommand]
    private void WindowsDefaults()
    {
        Edit(p =>
        {
            p.Speed = 10;
            p.Acceleration = true;
            p.ScrollLines = 3;
            p.DoubleClickMs = 500;
        });
        Load();
    }
}
