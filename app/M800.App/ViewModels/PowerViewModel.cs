using CommunityToolkit.Mvvm.ComponentModel;
using M800.Core.Model;

namespace M800.App.ViewModels;

public sealed partial class PowerViewModel : ObservableObject
{
    private readonly AppHost _host;
    private bool _loading;

    [ObservableProperty] private int _sleepMinutes;
    [ObservableProperty] private bool _lightOffWhileMoving;
    [ObservableProperty] private bool _motionWakeup;

    public PowerViewModel(AppHost host)
    {
        _host = host;
        host.ActiveProfileChanged += Load;
        Load();
    }

    public string SleepText => SleepMinutes == 1 ? "1 minuto" : $"{SleepMinutes} minutos";

    public void Load()
    {
        _loading = true;
        var p = _host.Active;
        SleepMinutes = Math.Clamp(p.SleepMinutes, 3, 10);
        LightOffWhileMoving = p.LightOffWhileMoving;
        MotionWakeup = p.MotionWakeup;
        _loading = false;
    }

    partial void OnSleepMinutesChanged(int value)
    {
        OnPropertyChanged(nameof(SleepText));
        if (!_loading)
            _host.Update(Section.Power, p => p.SleepMinutes = value);
    }

    partial void OnLightOffWhileMovingChanged(bool value)
    {
        if (!_loading)
            _host.Update(Section.Power, p => p.LightOffWhileMoving = value);
    }

    partial void OnMotionWakeupChanged(bool value)
    {
        if (!_loading)
            _host.Update(Section.MotionWakeup, p => p.MotionWakeup = value);
    }
}
