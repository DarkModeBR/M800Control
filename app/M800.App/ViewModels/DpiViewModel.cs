using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using M800.Core.Model;
using M800.Core.Protocol;
using M800.Core.Services;

namespace M800.App.ViewModels;

public sealed partial class DpiViewModel : ObservableObject
{
    private readonly AppHost _host;
    private SensorDpiTable? _table;
    private bool _editing;

    [ObservableProperty] private string _rangeText = "";

    public DpiViewModel(AppHost host)
    {
        _host = host;
        host.ActiveProfileChanged += Load;
        host.StatusChanged += () => { if (_table != _host.DpiTable) Load(); };
        host.ProfileEdited += () => { if (!_editing) Load(); };
        Load();
    }

    public ObservableCollection<DpiLevelItem> Levels { get; } = [];

    public void Load()
    {
        var table = _host.DpiTable;
        var profile = _host.Active;
        RangeText = $"Sensor {table.Name}: de {table.Min:N0} a {table.Max:N0} DPI. O botão de DPI do mouse alterna entre os níveis ligados (máximo 6).";
        if (_table != table || Levels.Count != profile.DpiLevels.Count)
        {
            _table = table;
            Levels.Clear();
            for (int i = 0; i < profile.DpiLevels.Count; i++)
                Levels.Add(new DpiLevelItem(this, i, table));
        }
        int active = ProfileEncoder.ActiveLevel(profile);
        for (int i = 0; i < Levels.Count; i++)
            Levels[i].Sync(profile.DpiLevels[i], i == active);
    }

    internal void SetEnabled(int index, bool enabled)
    {
        if (!enabled && _host.Active.DpiLevels.Count(l => l.Enabled) == 1)
        {
            Defer(Load); // mantém pelo menos um nível ligado
            return;
        }
        Edit(Section.Dpi, p =>
        {
            p.DpiLevels[index].Enabled = enabled;
            p.Normalize();
        });
        Defer(Load);
    }

    internal void SetActive(int index)
    {
        if (!_host.Active.DpiLevels[index].Enabled)
        {
            Defer(Load);
            return;
        }
        _editing = true;
        try
        {
            _host.SelectDpiLevel(index);
        }
        finally
        {
            _editing = false;
        }
        Defer(Load);
    }

    internal void SetDpi(int index, int dpi) => Edit(Section.Dpi, p => p.DpiLevels[index].Dpi = dpi);

    internal void SetColor(int index, Rgb color)
    {
        Edit(Section.DpiColors, p => p.DpiLevels[index].Color = color);
        _host.Mouse.Flash(color); // prévia da cor nova no LED
    }

    private void Edit(Section section, Action<Profile> change)
    {
        _editing = true;
        try
        {
            _host.Update(section, change);
        }
        finally
        {
            _editing = false;
        }
    }

    private static void Defer(Action action) => Application.Current.Dispatcher.BeginInvoke(action);
}

public sealed partial class DpiLevelItem : ObservableObject
{
    private readonly DpiViewModel _owner;
    private readonly SensorDpiTable _table;
    private bool _loading;

    [ObservableProperty] private bool _enabled;
    [ObservableProperty] private bool _isActive;
    [ObservableProperty] private int _sliderIndex;
    [ObservableProperty] private double? _dpiValue;
    [ObservableProperty] private Rgb _color;

    public DpiLevelItem(DpiViewModel owner, int index, SensorDpiTable table)
    {
        _owner = owner;
        _table = table;
        Index = index;
    }

    public int Index { get; }
    public string Title => $"Nível {Index + 1}";
    public int SliderMax => _table.Values.Length - 1;
    public double MinDpi => _table.Min;
    public double MaxDpi => _table.Max;

    internal void Sync(DpiLevel level, bool active)
    {
        _loading = true;
        int dpi = _table.Nearest(level.Dpi);
        Enabled = level.Enabled;
        IsActive = active;
        SliderIndex = _table.IndexOf(dpi);
        DpiValue = dpi;
        Color = level.Color;
        _loading = false;
    }

    partial void OnEnabledChanged(bool value)
    {
        if (!_loading)
            _owner.SetEnabled(Index, value);
    }

    partial void OnIsActiveChanged(bool value)
    {
        if (!_loading && value)
            _owner.SetActive(Index);
    }

    partial void OnSliderIndexChanged(int value)
    {
        if (_loading || value < 0 || value > SliderMax)
            return;
        int dpi = _table.Values[value];
        _loading = true;
        DpiValue = dpi;
        _loading = false;
        _owner.SetDpi(Index, dpi);
    }

    partial void OnDpiValueChanged(double? value)
    {
        if (_loading || value is null)
            return;
        int dpi = _table.Nearest((int)Math.Round(value.Value));
        _loading = true;
        SliderIndex = _table.IndexOf(dpi);
        DpiValue = dpi;
        _loading = false;
        _owner.SetDpi(Index, dpi);
    }

    partial void OnColorChanged(Rgb value)
    {
        if (!_loading)
            _owner.SetColor(Index, value);
    }
}
