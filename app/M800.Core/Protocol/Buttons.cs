namespace M800.Core.Protocol;

/// <summary>Índices dos botões no firmware.</summary>
public enum ButtonId { Left = 0, Right = 1, Middle = 2, Back = 3, Forward = 4, Dpi = 5 }

public static class ButtonIds
{
    public static readonly IReadOnlyList<ButtonId> All = Enum.GetValues<ButtonId>();

    public static string DisplayName(ButtonId b) => b switch
    {
        ButtonId.Left => "Botão esquerdo",
        ButtonId.Right => "Botão direito",
        ButtonId.Middle => "Botão do meio (roda)",
        ButtonId.Back => "Lateral voltar",
        ButtonId.Forward => "Lateral avançar",
        ButtonId.Dpi => "Botão de DPI",
        _ => b.ToString(),
    };
}

public static class MouseButtons
{
    public const byte Left = 0x01, Right = 0x02, Middle = 0x04, Back = 0x08, Forward = 0x10;
}

public static class Modifiers
{
    public const byte LCtrl = 0x01, LShift = 0x02, LAlt = 0x04, LWin = 0x08;
    public const byte RCtrl = 0x10, RShift = 0x20, RAlt = 0x40, RWin = 0x80;
}

public static class ConsumerKeys
{
    public const ushort MediaSelect = 0x0183, PrevTrack = 0x00B6, NextTrack = 0x00B5, Stop = 0x00B7,
        PlayPause = 0x00CD, Mute = 0x00E2, VolumeUp = 0x00E9, VolumeDown = 0x00EA,
        Mail = 0x018A, Calculator = 0x0192, MyComputer = 0x0194;
}

/// <summary>Ação de um botão: 3 bytes [tipo, b1, b2] (PROTOCOLO.md §6).</summary>
public readonly record struct ButtonAction(byte Type, byte B1, byte B2)
{
    public const byte TypeDisabled = 0x00, TypeMouse = 0x10, TypeFire = 0x30, TypeDpi = 0x40,
        TypeLighting = 0x60, TypeKey = 0x70, TypeConsumer = 0x80, TypeMacro = 0x90;

    public static ButtonAction Disabled => new(TypeDisabled, 0, 0);
    public static ButtonAction TripleClick => new(TypeFire, 0x32, 0x03);
    public static ButtonAction DpiLoop => new(TypeDpi, 0x01, 0);
    public static ButtonAction DpiUp => new(TypeDpi, 0x02, 0);
    public static ButtonAction DpiDown => new(TypeDpi, 0x03, 0);
    public static ButtonAction SwitchLighting => new(TypeLighting, 0x04, 0);

    public static ButtonAction Mouse(byte mask) => new(TypeMouse, mask, 0);

    public static ButtonAction Fire(int intervalMs, int repeat)
    {
        if (intervalMs is < 5 or > 255) throw new ArgumentOutOfRangeException(nameof(intervalMs), "intervalo de 5 a 255 ms");
        if (repeat is < 0 or > 255) throw new ArgumentOutOfRangeException(nameof(repeat), "repetições de 0 a 255");
        return new(TypeFire, (byte)intervalMs, (byte)repeat);
    }

    public static ButtonAction Key(byte hidUsage, byte modifiers = 0) => new(TypeKey, modifiers, hidUsage);

    public static ButtonAction Consumer(ushort usage) => new(TypeConsumer, (byte)(usage & 0xFF), (byte)(usage >> 8));

    /// <summary>Executa a macro do slot (1..15) <paramref name="repeat"/> vezes (modo 2, o único seguro).</summary>
    public static ButtonAction Macro(int slot, int repeat)
    {
        if (slot is < 1 or > 15) throw new ArgumentOutOfRangeException(nameof(slot), "slot de 1 a 15");
        if (repeat is < 1 or > 255) throw new ArgumentOutOfRangeException(nameof(repeat), "repetições de 1 a 255");
        return new(TypeMacro, (byte)(0x20 | slot), (byte)repeat);
    }

    public static ButtonAction DefaultFor(ButtonId button) => button switch
    {
        ButtonId.Left => Mouse(MouseButtons.Left),
        ButtonId.Right => Mouse(MouseButtons.Right),
        ButtonId.Middle => Mouse(MouseButtons.Middle),
        ButtonId.Back => Mouse(MouseButtons.Back),
        ButtonId.Forward => Mouse(MouseButtons.Forward),
        _ => DpiLoop,
    };
}
