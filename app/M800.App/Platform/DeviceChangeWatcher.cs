using System.Windows;
using System.Windows.Interop;

namespace M800.App.Platform;

/// <summary>Avisa quando o Windows anuncia mudança de dispositivos (cabo ou receptor ligado/desligado).</summary>
public static class DeviceChangeWatcher
{
    private const int WM_DEVICECHANGE = 0x0219;

    public static void Attach(Window window, Action onChange)
    {
        var source = HwndSource.FromHwnd(new WindowInteropHelper(window).EnsureHandle());
        source?.AddHook((IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) =>
        {
            if (msg == WM_DEVICECHANGE)
                onChange();
            return IntPtr.Zero;
        });
    }
}
