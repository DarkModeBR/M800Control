using System.Globalization;

namespace M800.Core.Protocol;

public readonly record struct Rgb(byte R, byte G, byte B)
{
    public static readonly Rgb Black = new(0, 0, 0);
    public static readonly Rgb White = new(255, 255, 255);

    public string ToHex() => $"#{R:X2}{G:X2}{B:X2}";

    public override string ToString() => ToHex();

    public static Rgb Parse(string text)
    {
        var s = text.Trim().TrimStart('#');
        if (s.Length != 6 || !int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v))
            throw new FormatException($"cor inválida: '{text}' (use #RRGGBB)");
        return new Rgb((byte)(v >> 16), (byte)(v >> 8), (byte)v);
    }

    public static bool TryParse(string text, out Rgb color)
    {
        try
        {
            color = Parse(text);
            return true;
        }
        catch (FormatException)
        {
            color = Black;
            return false;
        }
    }
}
