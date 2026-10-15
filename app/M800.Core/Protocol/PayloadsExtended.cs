namespace M800.Core.Protocol;

public static partial class Payloads
{
    public const int MaxMacroEvents = 125;
    public const int MaxMacroSlots = 15;

    /// <summary>[modo, (brilho&lt;&lt;4)|velocidade, dados do modo] (PROTOCOLO.md §5).</summary>
    public static byte[] Lighting(LightingMode mode, int brightness, int speed, IReadOnlyList<Rgb> colors, int direction)
    {
        if (brightness is < 1 or > 15 || speed is < 1 or > 15)
            throw new ArgumentOutOfRangeException(nameof(brightness), "brilho e velocidade de 1 a 15");

        var data = new List<byte> { (byte)mode, (byte)((brightness << 4) | speed) };
        switch (mode)
        {
            case LightingMode.ColorfulStreaming:
                data.Add((byte)(direction == 0 ? 0 : 1));
                break;
            case LightingMode.Breathing:
                var visible = colors.Where(c => c != Rgb.Black).Take(7).ToList();
                data.Add((byte)visible.Count);
                foreach (var c in visible) data.AddRange([c.R, c.G, c.B]);
                break;
            case LightingMode.Steady:
                var steady = colors.Count > 0 ? colors[0] : Rgb.White;
                data.AddRange([steady.R, steady.G, steady.B]);
                break;
            case LightingMode.ColorfulSteady:
                for (int i = 0; i < 7; i++)
                {
                    var c = i < colors.Count ? colors[i] : Rgb.Black;
                    data.AddRange([c.R, c.G, c.B]);
                }
                break;
            case LightingMode.Flicker:
                var block = new byte[21];
                for (int i = 0; i < 2 && i < colors.Count; i++)
                {
                    block[i * 3] = colors[i].R;
                    block[i * 3 + 1] = colors[i].G;
                    block[i * 3 + 2] = colors[i].B;
                }
                block[0] = 0; // o driver oficial zera este byte; mantido igual
                data.AddRange(block);
                break;
        }
        return data.ToArray();
    }

    public static byte[] Key(ButtonId button, ButtonAction action) =>
        [(byte)button, action.Type, action.B1, action.B2];

    /// <summary>Divide a macro em blocos: byte 5 = (bloco&lt;&lt;4)|total; payload = [slot] + até 25 bytes.</summary>
    public static IReadOnlyList<(byte Chunk, byte[] Payload)> MacroChunks(int slot, IReadOnlyList<MacroEvent> events)
    {
        if (slot is < 1 or > MaxMacroSlots)
            throw new ArgumentOutOfRangeException(nameof(slot), "slot de macro de 1 a 15");
        if (events.Count > MaxMacroEvents)
            throw new ArgumentException($"a macro tem {events.Count} eventos (máx. {MaxMacroEvents})", nameof(events));

        var bytes = events.SelectMany(e => e.Encode()).ToArray();
        int total = Math.Max(1, (bytes.Length + 24) / 25);
        var chunks = new List<(byte, byte[])>(total);
        for (int i = 0; i < total; i++)
        {
            var piece = bytes.Skip(i * 25).Take(25);
            chunks.Add(((byte)((i << 4) | total), [(byte)slot, .. piece]));
        }
        return chunks;
    }
}
