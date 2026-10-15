using System.Text;

namespace M800.Core.Protocol;

public enum ChargeState { Discharging, Charging, Full }

public sealed record DeviceInfo(string Model, byte SensorId, int Battery, ChargeState Charge, bool Online)
{
    public string SensorName =>
        ProtocolConstants.SensorNames.TryGetValue(SensorId, out var name) ? name : $"0x{SensorId:X2}";
}

/// <summary>Enviada pelo mouse quando o usuário troca DPI/polling no próprio mouse. DpiLevel é 0-based.</summary>
public sealed record DpiNotification(int PollingRateHz, int DpiLevel);

public static class Responses
{
    /// <returns>null quando o mouse ainda não respondeu (byte 6 = 0) — reenvie a consulta.</returns>
    public static DeviceInfo? ParseInfo(ReadOnlySpan<byte> r)
    {
        if (r.Length < 22 || r[0] != ProtocolConstants.ReportId || r[2] != (byte)Command.Info || r[3] != 0)
            throw new FormatException("resposta inválida para INFO: " + Convert.ToHexString(r));
        if (r[6] == 0)
            return null;

        var charge = r[19] switch
        {
            1 => ChargeState.Charging,
            2 => ChargeState.Full,
            _ => ChargeState.Discharging,
        };
        int battery = charge == ChargeState.Full ? 100 : Math.Clamp((int)r[18], 0, 100);
        return new DeviceInfo(Encoding.ASCII.GetString(r.Slice(8, 4)), r[15], battery, charge, r[21] != 0);
    }

    public static DpiNotification? ParseNotification(ReadOnlySpan<byte> r)
    {
        if (r.Length < 5 || r[0] != 0x05 || r[1] != 0x06)
            return null;
        return new DpiNotification(PollingFromCode(r[3]), r[4] - 1);
    }

    public static int PollingFromCode(byte code) => code switch
    {
        1 => 1000,
        2 => 500,
        4 => 250,
        8 => 125,
        _ => 0,
    };
}
