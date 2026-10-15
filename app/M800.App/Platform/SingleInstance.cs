namespace M800.App.Platform;

/// <summary>Garante uma única instância; uma segunda execução só pede para a primeira mostrar a janela.</summary>
public sealed class SingleInstance : IDisposable
{
    private const string MutexName = @"Local\M800Control.SingleInstance";
    private const string EventName = @"Local\M800Control.Show";
    private readonly Mutex _mutex;
    private readonly EventWaitHandle _showEvent;
    private volatile bool _disposed;

    private SingleInstance(Mutex mutex)
    {
        _mutex = mutex;
        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, EventName);
        new Thread(Listen) { IsBackground = true, Name = "M800 instância única" }.Start();
    }

    public event Action? ShowRequested;

    public static SingleInstance? TryAcquire()
    {
        var mutex = new Mutex(true, MutexName, out bool created);
        if (created)
            return new SingleInstance(mutex);
        mutex.Dispose();
        return null;
    }

    public static void SignalExisting()
    {
        try
        {
            using var existing = EventWaitHandle.OpenExisting(EventName);
            existing.Set();
        }
        catch (WaitHandleCannotBeOpenedException)
        {
        }
    }

    private void Listen()
    {
        while (!_disposed)
        {
            if (_showEvent.WaitOne(1000) && !_disposed)
                ShowRequested?.Invoke();
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
