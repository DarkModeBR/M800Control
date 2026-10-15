using System.Numerics;

namespace M800.Core.Protocol;

/// <summary>Dados (a partir do byte 7) de cada comando. Ver PROTOCOLO.md §3–§7.</summary>
public static partial class Payloads
{
    public static readonly int[] PollingRates = [125, 250, 500, 1000];

    public static byte[] PollingRate(int hz) => hz switch
    {
        1000 => new byte[] { 0x01 },
        500 => new byte[] { 0x02 },
        250 => new byte[] { 0x04 },
        125 => new byte[] { 0x08 },
        _ => throw new ArgumentOutOfRangeException(nameof(hz), "polling deve ser 125, 250, 500 ou 1000 Hz"),
    };

    public static byte[] LiftOff(int mm)
    {
        if (mm is not (1 or 2))
            throw new ArgumentOutOfRangeException(nameof(mm), "LOD deve ser 1 ou 2 mm");
        return new[] { (byte)mm };
    }

    public static byte[] Debounce(int ms)
    {
        if (ms is < 0 or > 30 || ms % 2 != 0)
            throw new ArgumentOutOfRangeException(nameof(ms), "debounce deve ser par, de 0 a 30 ms");
        return new[] { (byte)ms };
    }

    public static byte[] Power(int sleepMinutes, bool lightOffWhileMoving)
    {
        if (sleepMinutes is < 1 or > 255)
            throw new ArgumentOutOfRangeException(nameof(sleepMinutes), "tempo para dormir de 1 a 255 min");
        return new[] { (byte)sleepMinutes, (byte)(lightOffWhileMoving ? 1 : 0) };
    }

    public static byte[] MotionWakeup(bool enabled) => new[] { (byte)(enabled ? 1 : 0) };

    public static byte[] FactoryReset() => new byte[] { 0xFF };

    /// <summary>[((nível+1)&lt;&lt;4)|qtd, máscara, 8 × registrador LE].</summary>
    public static byte[] Dpi(IReadOnlyList<ushort> registers, int currentLevel, byte enabledMask)
    {
        if (registers.Count is < 1 or > 8)
            throw new ArgumentOutOfRangeException(nameof(registers), "de 1 a 8 níveis");
        if (currentLevel is < 0 or > 7)
            throw new ArgumentOutOfRangeException(nameof(currentLevel));

        int count = BitOperations.PopCount(enabledMask);
        var data = new byte[18];
        data[0] = (byte)(((currentLevel + 1) << 4) | (count & 0x0F));
        data[1] = enabledMask;
        for (int i = 0; i < registers.Count; i++)
        {
            data[2 + i * 2] = (byte)(registers[i] & 0xFF);
            data[3 + i * 2] = (byte)(registers[i] >> 8);
        }
        return data;
    }

    public static byte[] DpiColors(IReadOnlyList<Rgb> colors)
    {
        var data = new byte[24];
        for (int i = 0; i < 8 && i < colors.Count; i++)
        {
            data[i * 3] = colors[i].R;
            data[i * 3 + 1] = colors[i].G;
            data[i * 3 + 2] = colors[i].B;
        }
        return data;
    }
}
