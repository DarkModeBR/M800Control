namespace M800.Core.Protocol;

/// <summary>Constantes do protocolo HID do M800 PRO (ver PROTOCOLO.md).</summary>
public static class ProtocolConstants
{
    public const ushort VendorId = 0x248A;
    public const ushort ProductIdWired = 0x5B2E;
    public const ushort ProductIdDongle = 0x5B2F;
    public const byte ReportId = 0x0C;
    public const int ReportLength = 33;
    public const int MaxPayload = ReportLength - 7;

    /// <summary>Testado no mouse: com 8 níveis habilitados ele cicla só de 1 a 6.</summary>
    public const int MaxDpiLevels = 6;

    public static readonly TimeSpan InterPacketDelay = TimeSpan.FromMilliseconds(60);

    public static readonly IReadOnlyDictionary<byte, string> SensorNames = new Dictionary<byte, string>
    {
        [0x25] = "PAW3325",
        [0x27] = "PAW3327",
        [0x35] = "PAW3335",
        [0x60] = "PMW3360",
        [0x70] = "PAW3370",
        [0x89] = "PMW3389",
    };
}
