using M800.Core.Hid;
using M800.Core.Protocol;
using M800.Core.Services;

namespace M800.Core.Tests;

internal sealed class FakeTransport : IMouseTransport
{
    public ushort ProductId => ProtocolConstants.ProductIdDongle;
    public List<byte[]> Sent { get; } = [];
    public Queue<byte[]> InfoResponses { get; } = new();
    public byte[] DefaultInfo { get; set; } = Info(online: true);
    public int FailNextSends { get; set; }
    public bool Disposed { get; private set; }

    public IEnumerable<Command> Commands => Sent.Select(p => (Command)p[2]).Where(c => c != Command.Info);

    public static byte[] Info(bool online, int battery = 80, byte charge = 0)
    {
        var r = Convert.FromHexString(PacketTests.WiredInfoHex);
        r[18] = (byte)battery;
        r[19] = charge;
        r[21] = (byte)(online ? 1 : 0);
        return r;
    }

    public void SetFeature(byte[] report)
    {
        if (FailNextSends > 0)
        {
            FailNextSends--;
            throw new IOException("falha simulada");
        }
        Sent.Add(report);
    }

    public byte[] GetFeature() => InfoResponses.Count > 0 ? InfoResponses.Dequeue() : DefaultInfo;

    public byte[]? ReadNotification(int timeoutMs) => null;

    public void Dispose() => Disposed = true;
}

internal sealed class FakeFactory : IMouseTransportFactory
{
    public FakeTransport? Next { get; set; } = new();
    public int Opens { get; private set; }

    public IMouseTransport? TryOpen()
    {
        Opens++;
        return Next;
    }
}

internal sealed class ListLog : IPacketLog
{
    public List<string> Lines { get; } = [];
    public void Write(string line) => Lines.Add(line);
}
