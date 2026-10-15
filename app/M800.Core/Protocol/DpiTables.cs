namespace M800.Core.Protocol;

/// <summary>Valores de DPI aceitos por um sensor. Registrador enviado = índice + RegisterOffset.</summary>
public sealed record SensorDpiTable(string Name, int RegisterOffset, int[] Values)
{
    public int Min => Values[0];

    public int Max => Values[^1];

    public int IndexOf(int dpi) => Array.IndexOf(Values, dpi);

    public ushort ToRegister(int dpi)
    {
        int index = IndexOf(dpi);
        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(dpi), $"{dpi} DPI não é suportado pelo {Name}");
        return (ushort)(index + RegisterOffset);
    }

    public int Nearest(int dpi)
    {
        int best = Values[0];
        foreach (var v in Values)
        {
            if (Math.Abs(v - dpi) < Math.Abs(best - dpi))
                best = v;
        }
        return best;
    }
}

public static partial class DpiTables
{
    public const byte Paw3370 = 0x70;

    public static SensorDpiTable Get(byte sensorId) =>
        All.TryGetValue(sensorId, out var table) ? table : All[Paw3370];
}
