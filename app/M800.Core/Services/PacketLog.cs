namespace M800.Core.Services;

public interface IPacketLog
{
    void Write(string line);
}

public sealed class NullPacketLog : IPacketLog
{
    public static readonly NullPacketLog Instance = new();
    public void Write(string line) { }
}

/// <summary>Log em texto com rotação: acima de 1 MB o arquivo vira log.old.txt.</summary>
public sealed class FilePacketLog(string path) : IPacketLog
{
    private const long MaxBytes = 1024 * 1024;
    private readonly object _lock = new();

    public string Path { get; } = path;

    public void Write(string line)
    {
        lock (_lock)
        {
            try
            {
                var info = new FileInfo(Path);
                if (info.Exists && info.Length > MaxBytes)
                    File.Move(Path, System.IO.Path.ChangeExtension(Path, ".old.txt"), overwrite: true);
                File.AppendAllText(Path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {line}{Environment.NewLine}");
            }
            catch (IOException)
            {
                // log nunca derruba o app
            }
        }
    }
}
