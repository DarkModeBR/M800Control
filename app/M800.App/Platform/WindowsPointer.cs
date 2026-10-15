using System.Runtime.InteropServices;
using M800.Core.Model;

namespace M800.App.Platform;

/// <summary>Lê e aplica as configurações do ponteiro do Windows (as mesmas do Painel de Controle).</summary>
public static class WindowsPointer
{
    private const uint SPI_GETMOUSE = 0x0003, SPI_SETMOUSE = 0x0004, SPI_SETDOUBLECLICKTIME = 0x0020,
        SPI_GETWHEELSCROLLLINES = 0x0068, SPI_SETWHEELSCROLLLINES = 0x0069,
        SPI_GETMOUSESPEED = 0x0070, SPI_SETMOUSESPEED = 0x0071;
    private const uint SPIF_UPDATEINIFILE = 0x01, SPIF_SENDCHANGE = 0x02;
    private const uint WheelPageScroll = uint.MaxValue; // "uma tela por vez"

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfo(uint action, uint param, IntPtr value, uint winIni);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfo(uint action, uint param, int[] value, uint winIni);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfo(uint action, uint param, ref uint value, uint winIni);

    [DllImport("user32.dll")]
    private static extern uint GetDoubleClickTime();

    public static PointerSettings ReadCurrent()
    {
        uint speed = 10;
        SystemParametersInfo(SPI_GETMOUSESPEED, 0, ref speed, 0);
        var mouse = new int[3];
        SystemParametersInfo(SPI_GETMOUSE, 0, mouse, 0);
        uint lines = 3;
        SystemParametersInfo(SPI_GETWHEELSCROLLLINES, 0, ref lines, 0);
        var settings = new PointerSettings
        {
            Speed = (int)speed,
            Acceleration = mouse[2] != 0,
            ScrollLines = lines == WheelPageScroll ? 0 : (int)lines,
            DoubleClickMs = (int)GetDoubleClickTime(),
        };
        settings.Normalize();
        return settings;
    }

    public static void Apply(PointerSettings s)
    {
        const uint flags = SPIF_UPDATEINIFILE | SPIF_SENDCHANGE;
        SystemParametersInfo(SPI_SETMOUSESPEED, 0, s.Speed, flags);
        // limiares e nível de aceleração usados pelo Windows quando "Aumentar precisão do ponteiro" está ligado
        SystemParametersInfo(SPI_SETMOUSE, 0, s.Acceleration ? new[] { 6, 10, 1 } : new[] { 0, 0, 0 }, flags);
        SystemParametersInfo(SPI_SETWHEELSCROLLLINES, s.ScrollLines == 0 ? WheelPageScroll : (uint)s.ScrollLines, IntPtr.Zero, flags);
        SystemParametersInfo(SPI_SETDOUBLECLICKTIME, (uint)s.DoubleClickMs, IntPtr.Zero, flags);
    }
}
