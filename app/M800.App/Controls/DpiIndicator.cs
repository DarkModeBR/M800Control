using M800.Core.Services;

namespace M800.App.Controls;

/// <summary>Mostra o indicador de DPI quando o nível ativo muda (se ligado nas Configurações).</summary>
public sealed class DpiIndicator
{
    private readonly AppHost _host;
    private DpiIndicatorWindow? _window;

    public DpiIndicator(AppHost host)
    {
        _host = host;
        host.ActiveDpiChanged += Show;
    }

    private void Show()
    {
        if (!_host.Settings.DpiIndicatorEnabled)
            return;
        var profile = _host.Active;
        int active = ProfileEncoder.ActiveLevel(profile);
        var enabled = Enumerable.Range(0, profile.DpiLevels.Count).Where(i => profile.DpiLevels[i].Enabled).ToList();
        var level = profile.DpiLevels[active];
        _window ??= new DpiIndicatorWindow();
        _window.ShowLevel(level.Dpi, level.Color, enabled.IndexOf(active) + 1, enabled.Count);
    }
}
