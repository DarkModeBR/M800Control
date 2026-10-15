namespace M800.Core.Hid;

public interface IMouseTransport : IDisposable
{
    ushort ProductId { get; }

    /// <exception cref="IOException">quando o envio falha.</exception>
    void SetFeature(byte[] report);

    /// <exception cref="IOException">quando a leitura falha.</exception>
    byte[] GetFeature();

    /// <returns>o relatório recebido, ou null se nada chegou dentro do tempo.</returns>
    byte[]? ReadNotification(int timeoutMs);
}

public interface IMouseTransportFactory
{
    IMouseTransport? TryOpen();
}
