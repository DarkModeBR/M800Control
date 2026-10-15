using M800.Core.Hid;
using M800.Core.Protocol;

namespace M800.Core.Tests;

/// <summary>Só executa com o mouse conectado: defina M800_HW=1. Não altera nada no mouse.</summary>
public class HardwareTests
{
    private static bool Enabled => Environment.GetEnvironmentVariable("M800_HW") == "1";

    [Fact]
    public void Finds_mouse_and_reads_info()
    {
        if (!Enabled) return;

        using var transport = new HidMouseTransportFactory().TryOpen();
        Assert.NotNull(transport);
        DeviceInfo? info = null;
        for (int i = 0; i < 3 && info is null; i++)
        {
            transport.SetFeature(Packets.InfoRequest((byte)i));
            Thread.Sleep(10);
            info = Responses.ParseInfo(transport.GetFeature());
        }
        Assert.NotNull(info);
        Assert.Equal("M802", info.Model);
        Assert.Equal("PAW3370", info.SensorName);
    }
}
