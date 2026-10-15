using System.Windows;
using System.Windows.Media;
using M800.Core.Model;
using Microsoft.Win32;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace M800.App.Common;

public static class ThemeService
{
    /// <summary>Azul padrão do Windows 11, usado quando o destaque do sistema é cinza.</summary>
    private static readonly Color Windows11Blue = Color.FromRgb(0x00, 0x78, 0xD4);

    private static AppTheme _current = AppTheme.System;

    public static void Apply(AppTheme theme, Window? window = null)
    {
        _current = theme;
        if (window is { IsLoaded: true })
        {
            try
            {
                SystemThemeWatcher.UnWatch(window);
            }
            catch (InvalidOperationException)
            {
                // a janela não estava sendo vigiada
            }
        }

        var target = theme switch
        {
            AppTheme.Light => ApplicationTheme.Light,
            AppTheme.Dark => ApplicationTheme.Dark,
            _ => SystemAppsUseDarkTheme() ? ApplicationTheme.Dark : ApplicationTheme.Light,
        };
        // O destaque precisa vir ANTES do dicionário do tema: os pincéis do tema (interruptores,
        // botões primários...) copiam as cores de destaque no momento em que o dicionário carrega.
        ApplicationAccentColorManager.Apply(PreferredAccent(), target);
        ApplicationThemeManager.Apply(target, WindowBackdropType.Mica, false);

        if (theme == AppTheme.System && window is not null)
        {
            // o WPF-UI só aceita vigiar janelas já carregadas
            if (window.IsLoaded)
                Watch(window);
            else
                window.Loaded += WatchWhenLoaded;
        }
    }

    private static void Watch(Window window) => SystemThemeWatcher.Watch(window, WindowBackdropType.Mica, false);

    private static void WatchWhenLoaded(object sender, RoutedEventArgs e)
    {
        var window = (Window)sender;
        window.Loaded -= WatchWhenLoaded;
        if (_current == AppTheme.System)
            Watch(window);
    }

    /// <summary>Destaque do Windows; se for um tom de cinza o item ativo some na interface, então usa o azul padrão.</summary>
    private static Color PreferredAccent()
    {
        var system = ApplicationAccentColorManager.GetColorizationColor();
        return Saturation(system) < 0.15 ? Windows11Blue : system;
    }

    private static bool SystemAppsUseDarkTheme()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("AppsUseLightTheme") is not int value || value == 0;
    }

    private static double Saturation(Color c)
    {
        double max = Math.Max(c.R, Math.Max(c.G, c.B)) / 255.0;
        double min = Math.Min(c.R, Math.Min(c.G, c.B)) / 255.0;
        return max == 0 ? 0 : (max - min) / max;
    }
}
