using System.Runtime.InteropServices;
using System.Text;
using M800.Core.Protocol;

namespace M800.App.Platform;

/// <summary>Nome da tecla no layout de teclado do Windows (ex.: "Ç" no ABNT2).</summary>
public static class KeyNames
{
    private static readonly HashSet<int> ExtendedKeys =
        [0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x27, 0x28, 0x2C, 0x2D, 0x2E, 0x5B, 0x5C, 0x5D, 0x6F, 0x90, 0xA3, 0xA5];

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint code, uint mapType);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetKeyNameText(int lParam, StringBuilder buffer, int size);

    public static string Name(byte hidUsage)
    {
        int vk = KeyMap.HidToVk(hidUsage);
        if (vk == 0)
            return $"Tecla 0x{hidUsage:X2}";
        uint scan = MapVirtualKey((uint)vk, 0);
        int lParam = (int)(scan << 16) | (ExtendedKeys.Contains(vk) ? 1 << 24 : 0);
        var buffer = new StringBuilder(64);
        return GetKeyNameText(lParam, buffer, buffer.Capacity) > 0 ? buffer.ToString() : $"Tecla 0x{hidUsage:X2}";
    }
}
