using System.Runtime.InteropServices;
using M800.Core.Protocol;
using Microsoft.Win32.SafeHandles;
using static M800.Core.Hid.NativeMethods;

namespace M800.Core.Hid;

public sealed class HidMouseTransport : IMouseTransport
{
    private readonly SafeFileHandle _config;
    private readonly SafeFileHandle? _notify;
    private readonly IntPtr _readBuffer;
    private readonly IntPtr _overlapped;
    private readonly ManualResetEvent _readEvent = new(false);
    private readonly object _readLock = new();
    private bool _disposed;

    public HidMouseTransport(ushort productId, string configPath, string? notifyPath)
    {
        ProductId = productId;
        _config = Open(configPath, 0);
        if (notifyPath is not null)
        {
            try
            {
                _notify = Open(notifyPath, FILE_FLAG_OVERLAPPED);
            }
            catch (IOException)
            {
                _notify = null; // sem notificações, o resto funciona
            }
        }
        _readBuffer = Marshal.AllocHGlobal(ProtocolConstants.ReportLength);
        _overlapped = Marshal.AllocHGlobal(Marshal.SizeOf<NativeOverlapped>());
    }

    public ushort ProductId { get; }

    private static SafeFileHandle Open(string path, uint flags)
    {
        var handle = CreateFile(path, GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE,
            IntPtr.Zero, OPEN_EXISTING, flags, IntPtr.Zero);
        if (handle.IsInvalid)
            throw new IOException($"não foi possível abrir o mouse (erro {Marshal.GetLastWin32Error()})");
        return handle;
    }

    public void SetFeature(byte[] report)
    {
        if (!HidD_SetFeature(_config, report, report.Length))
            throw new IOException($"falha ao enviar para o mouse (erro {Marshal.GetLastWin32Error()})");
    }

    public byte[] GetFeature()
    {
        var buffer = new byte[ProtocolConstants.ReportLength];
        buffer[0] = ProtocolConstants.ReportId;
        if (!HidD_GetFeature(_config, buffer, buffer.Length))
            throw new IOException($"falha ao ler do mouse (erro {Marshal.GetLastWin32Error()})");
        return buffer;
    }

    public byte[]? ReadNotification(int timeoutMs)
    {
        if (_notify is null)
        {
            Thread.Sleep(timeoutMs);
            return null;
        }
        lock (_readLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _readEvent.Reset();
            var overlapped = new NativeOverlapped { EventHandle = _readEvent.SafeWaitHandle.DangerousGetHandle() };
            Marshal.StructureToPtr(overlapped, _overlapped, false);
            if (!ReadFile(_notify, _readBuffer, ProtocolConstants.ReportLength, IntPtr.Zero, _overlapped))
            {
                int error = Marshal.GetLastWin32Error();
                if (error != ERROR_IO_PENDING)
                    throw new IOException($"falha na leitura de notificações (erro {error})");
            }
            if (!_readEvent.WaitOne(timeoutMs))
            {
                CancelIoEx(_notify, _overlapped);
                GetOverlappedResult(_notify, _overlapped, out _, true);
                return null;
            }
            if (!GetOverlappedResult(_notify, _overlapped, out int read, false))
                throw new IOException($"falha na leitura de notificações (erro {Marshal.GetLastWin32Error()})");
            var data = new byte[read];
            Marshal.Copy(_readBuffer, data, 0, read);
            return data;
        }
    }

    public void Dispose()
    {
        lock (_readLock)
        {
            if (_disposed)
                return;
            _disposed = true;
            _config.Dispose();
            _notify?.Dispose();
            Marshal.FreeHGlobal(_readBuffer);
            Marshal.FreeHGlobal(_overlapped);
            _readEvent.Dispose();
        }
    }
}
