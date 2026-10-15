using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace M800.App.Tray;

/// <summary>Desenha o ícone da bandeja: uma bateria com o nível colorido (vermelho abaixo do limite do alerta).</summary>
public static class BatteryIconRenderer
{
    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);

    public static Icon Render(int? battery, bool charging, int threshold)
    {
        var ink = battery is null ? Color.Gray : TaskbarIsLight() ? Color.FromArgb(32, 32, 32) : Color.White;
        using var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(ink, 2.5f);
            using var body = RoundedRectangle(new RectangleF(2, 8, 24, 16), 3);
            g.DrawPath(pen, body);
            using var terminal = new SolidBrush(ink);
            g.FillRectangle(terminal, 27, 13, 3, 6);
            if (battery is int level)
            {
                var color = level <= threshold ? Color.FromArgb(232, 17, 35)
                    : charging ? Color.FromArgb(0x60, 0xCD, 0xFF)
                    : level <= 40 ? Color.FromArgb(255, 185, 0)
                    : Color.FromArgb(16, 185, 80);
                using var fill = new SolidBrush(color);
                g.FillRectangle(fill, 4.5f, 10.5f, Math.Max(2f, 19f * level / 100f), 11f);
            }
        }
        IntPtr handle = bitmap.GetHicon();
        try
        {
            return (Icon)Icon.FromHandle(handle).Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    private static GraphicsPath RoundedRectangle(RectangleF r, float radius)
    {
        float d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static bool TaskbarIsLight()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("SystemUsesLightTheme") is int value && value == 1;
    }
}
