namespace M800.Core.Protocol;

/// <summary>
/// Teclas virtuais do Windows → usos HID. Tabela do driver oficial (0x61E1E0) com correções:
/// Alt direito (VK 0xA5) → 0xE6, teclas ABNT2 (VK 0xC1 → 0x87, VK 0xE2 → 0x64) e sem os
/// códigos internos de mouse do driver (VK 0xE0..0xE7).
/// </summary>
public static class KeyMap
{
    private static readonly byte[] VkToHidTable = Convert.FromHexString(
        "00000000000000002A2B000000285800" + // 0x00
        "00000048390000000000002900000000" + // 0x10
        "2C4B4E4D4A50524F5100000046494C00" + // 0x20
        "271E1F20212223242526000000000000" + // 0x30
        "000405060708090A0B0C0D0E0F101112" + // 0x40
        "131415161718191A1B1C1DE3E7650082" + // 0x50
        "62595A5B5C5D5E5F6061555700566354" + // 0x60
        "3A3B3C3D3E3F40414243444500000000" + // 0x70
        "00000000000000000000000000000000" + // 0x80
        "53470000000000000000000000000000" + // 0x90
        "E1E5E0E4E2E600000000000000000000" + // 0xA0 (A5 = Alt dir. corrigido)
        "00000000000000000000332E362D3738" + // 0xB0
        "35870000000000000000000000000000" + // 0xC0 (C1 = ABNT2 "/")
        "00000000000000000000002F31303400" + // 0xD0
        "00006400000000000000000000000000" + // 0xE0 (E2 = ABNT2 "\\")
        "81833264858788898A8B909100000000"); // 0xF0

    public static byte VkToHid(int vk) => vk is >= 0 and < 256 ? VkToHidTable[vk] : (byte)0;

    public static int HidToVk(byte hid)
    {
        if (hid == 0) return 0;
        int vk = Array.IndexOf(VkToHidTable, hid);
        return vk < 0 ? 0 : vk;
    }

    public static bool IsModifier(byte hid) => hid is >= 0xE0 and <= 0xE7;

    public static byte ModifierMask(byte hid) => IsModifier(hid) ? (byte)(1 << (hid - 0xE0)) : (byte)0;

    public static (MacroEventKind Kind, byte Code)? MacroCodeForVk(int vk)
    {
        byte hid = VkToHid(vk);
        if (hid == 0 || hid >= 0xF0)
            return null;
        return IsModifier(hid) ? (MacroEventKind.Modifier, ModifierMask(hid)) : (MacroEventKind.Key, hid);
    }

    private static readonly string[] ModifierLabels = ["Ctrl", "Shift", "Alt", "Win", "Ctrl dir.", "Shift dir.", "Alt dir.", "Win dir."];

    public static IReadOnlyList<string> ModifierNames(byte mask) =>
        Enumerable.Range(0, 8).Where(i => (mask & (1 << i)) != 0).Select(i => ModifierLabels[i]).ToList();
}
