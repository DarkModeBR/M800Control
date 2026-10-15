using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using M800.Core.Model;
using M800.Core.Protocol;

namespace M800.App.ViewModels;

public sealed record ModeItem(LightingMode Mode, string Name);

public sealed partial class LightingViewModel : ObservableObject
{
    private readonly AppHost _host;
    private bool _loading;

    [ObservableProperty] private ModeItem? _selectedMode;
    [ObservableProperty] private int _brightness;
    [ObservableProperty] private int _speed;
    [ObservableProperty] private bool _reverseDirection;
    [ObservableProperty] private bool _showSpeedAndBrightness;
    [ObservableProperty] private bool _showDirection;
    [ObservableProperty] private bool _showColors;
    [ObservableProperty] private string _colorsHint = "";

    public LightingViewModel(AppHost host)
    {
        _host = host;
        host.ActiveProfileChanged += Load;
        Load();
    }

    public IReadOnlyList<ModeItem> Modes { get; } =
        LightingModes.All.Select(m => new ModeItem(m, LightingModes.DisplayName(m))).ToList();

    public ObservableCollection<LightingColorItem> Colors { get; } = [];

    public void Load()
    {
        _loading = true;
        var lighting = _host.Active.Lighting;
        SelectedMode = Modes.First(m => m.Mode == lighting.Mode);
        Brightness = lighting.Brightness;
        Speed = lighting.Speed;
        ReverseDirection = lighting.Direction != 0;
        _loading = false;
        RefreshModeUi();
    }

    private void RefreshModeUi()
    {
        var mode = _host.Active.Lighting.Mode;
        ShowSpeedAndBrightness = LightingModes.UsesSpeedAndBrightness(mode);
        ShowDirection = LightingModes.UsesDirection(mode);
        ShowColors = LightingModes.MaxColors(mode) > 0;
        ColorsHint = mode switch
        {
            LightingMode.Breathing => "De 1 a 7 cores, que se alternam na respiração.",
            LightingMode.Steady => "Uma cor fixa.",
            LightingMode.ColorfulSteady => "7 cores fixas, uma por região do LED.",
            LightingMode.Flicker => "Duas cores alternadas.",
            _ => "",
        };

        var colors = EnsureColorCount(mode);
        Colors.Clear();
        for (int i = 0; i < colors.Count; i++)
            Colors.Add(new LightingColorItem(this, i, colors[i]));
        AddColorCommand.NotifyCanExecuteChanged();
        RemoveColorCommand.NotifyCanExecuteChanged();
    }

    private List<Rgb> EnsureColorCount(LightingMode mode)
    {
        var colors = _host.Active.Lighting.ColorsFor(mode);
        int min = LightingModes.MinColors(mode), max = LightingModes.MaxColors(mode);
        while (colors.Count < min)
            colors.Add(Rgb.White);
        if (colors.Count > max)
            colors.RemoveRange(max, colors.Count - max);
        return colors;
    }

    partial void OnSelectedModeChanged(ModeItem? value)
    {
        if (_loading || value is null)
            return;
        _host.Update(Section.Lighting, p => p.Lighting.Mode = value.Mode);
        RefreshModeUi();
    }

    partial void OnBrightnessChanged(int value)
    {
        if (!_loading)
            _host.Update(Section.Lighting, p => p.Lighting.Brightness = value);
    }

    partial void OnSpeedChanged(int value)
    {
        if (!_loading)
            _host.Update(Section.Lighting, p => p.Lighting.Speed = value);
    }

    partial void OnReverseDirectionChanged(bool value)
    {
        if (!_loading)
            _host.Update(Section.Lighting, p => p.Lighting.Direction = value ? 1 : 0);
    }

    internal void SetColor(int index, Rgb color) =>
        _host.Update(Section.Lighting, p => p.Lighting.ColorsFor(p.Lighting.Mode)[index] = color);

    private bool CanAddColor() =>
        _host.Active.Lighting.Mode == LightingMode.Breathing && Colors.Count < LightingModes.MaxColors(LightingMode.Breathing);

    private bool CanRemoveColor() =>
        _host.Active.Lighting.Mode == LightingMode.Breathing && Colors.Count > LightingModes.MinColors(LightingMode.Breathing);

    [RelayCommand(CanExecute = nameof(CanAddColor))]
    private void AddColor()
    {
        _host.Update(Section.Lighting, p => p.Lighting.ColorsFor(LightingMode.Breathing).Add(Rgb.White));
        RefreshModeUi();
    }

    [RelayCommand(CanExecute = nameof(CanRemoveColor))]
    private void RemoveColor()
    {
        _host.Update(Section.Lighting, p =>
        {
            var colors = p.Lighting.ColorsFor(LightingMode.Breathing);
            colors.RemoveAt(colors.Count - 1);
        });
        RefreshModeUi();
    }
}

public sealed partial class LightingColorItem : ObservableObject
{
    private readonly LightingViewModel _owner;
    private readonly int _index;
    private readonly bool _ready;

    [ObservableProperty] private Rgb _color;

    public LightingColorItem(LightingViewModel owner, int index, Rgb color)
    {
        _owner = owner;
        _index = index;
        Color = color;
        _ready = true;
    }

    partial void OnColorChanged(Rgb value)
    {
        if (_ready)
            _owner.SetColor(_index, value);
    }
}
