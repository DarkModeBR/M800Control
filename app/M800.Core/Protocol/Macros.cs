namespace M800.Core.Protocol;

public enum MacroEventKind : byte { Mouse = 0x1, Modifier = 0x2, Key = 0x3 }

/// <summary>Evento de macro. DelayMs = espera DEPOIS do evento (0..4095).</summary>
public sealed record MacroEvent(MacroEventKind Kind, byte Code, bool Release, int DelayMs)
{
    public const int MaxDelayMs = 0xFFF;

    public byte[] Encode()
    {
        int delay = Math.Clamp(DelayMs, 0, MaxDelayMs);
        return
        [
            (byte)((Release ? 0x80 : 0) | ((byte)Kind << 4) | (delay >> 8)),
            (byte)(delay & 0xFF),
            Code,
        ];
    }
}
