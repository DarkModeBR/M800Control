using System.Text.Json;
using System.Text.Json.Serialization;
using M800.Core.Protocol;

namespace M800.Core.Model;

public static class Json
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new RgbJsonConverter(), new JsonStringEnumConverter() },
    };
}

/// <summary>Grava cores como "#RRGGBB".</summary>
public sealed class RgbJsonConverter : JsonConverter<Rgb>
{
    public override Rgb Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        try
        {
            return Rgb.Parse(reader.GetString() ?? "");
        }
        catch (FormatException ex)
        {
            throw new JsonException(ex.Message, ex);
        }
    }

    public override void Write(Utf8JsonWriter writer, Rgb value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToHex());
}
