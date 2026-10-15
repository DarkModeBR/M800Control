namespace M800.Core.Protocol;

/// <summary>Monta o feature report de 33 bytes: [0x0C, 0x01, cmd, 0x00, seq, chunk, len, dados...].</summary>
public static class Packets
{
    public static byte[] Build(Command command, ReadOnlySpan<byte> payload, byte sequence, byte chunk = 0x01)
    {
        if (payload.Length > ProtocolConstants.MaxPayload)
            throw new ArgumentException(
                $"payload de {payload.Length} bytes (máx. {ProtocolConstants.MaxPayload})", nameof(payload));

        var packet = new byte[ProtocolConstants.ReportLength];
        packet[0] = ProtocolConstants.ReportId;
        packet[1] = 0x01;
        packet[2] = (byte)command;
        packet[4] = sequence;
        packet[5] = chunk;
        packet[6] = (byte)payload.Length;
        payload.CopyTo(packet.AsSpan(7));
        return packet;
    }

    /// <summary>Consulta INFO: o driver oficial zera os bytes 5 e 6.</summary>
    public static byte[] InfoRequest(byte sequence) =>
        Build(Command.Info, ReadOnlySpan<byte>.Empty, sequence, chunk: 0x00);
}
