using M800.Core.Hid;
using M800.Core.Model;
using M800.Core.Protocol;

namespace M800.Core.Services;

public enum ConnectionState { Disconnected, Sleeping, Online }

/// <summary>
/// Única porta para o mouse. Uma thread de trabalho conecta, consulta o estado e envia as seções
/// pendentes (coalescidas: vale a última versão de cada seção); outra thread lê as notificações.
/// </summary>
public sealed class MouseService : IDisposable
{
    public static readonly TimeSpan ReconnectInterval = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan InfoInterval = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan ShortInfoInterval = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan MaxInfoAgeBeforeSend = TimeSpan.FromSeconds(2);

    /// <summary>Um pedido de piscar mais antigo que isso é descartado (ex.: o mouse estava dormindo).</summary>
    public static readonly TimeSpan FlashMaxAge = TimeSpan.FromSeconds(5);

    private readonly IMouseTransportFactory _factory;
    private readonly IPacketLog _log;
    private readonly Action<TimeSpan> _sleep;
    private readonly object _lock = new();
    private readonly Dictionary<Section, long> _pending = [];
    private readonly AutoResetEvent _wake = new(false);
    private long _version;
    private Profile? _profile;
    private bool _resetRequested;
    private (Rgb Color, DateTime At)? _flash;
    private volatile IMouseTransport? _transport;
    private DateTime _nextConnect = DateTime.MinValue;
    private DateTime _nextInfo = DateTime.MinValue;
    private DateTime _lastInfo = DateTime.MinValue;
    private byte _sequence;
    private volatile bool _disposed;
    private Thread? _worker;
    private Thread? _reader;

    public MouseService(IMouseTransportFactory factory, IPacketLog log, Action<TimeSpan>? sleep = null)
    {
        _factory = factory;
        _log = log;
        _sleep = sleep ?? Thread.Sleep;
    }

    public ConnectionState State { get; private set; } = ConnectionState.Disconnected;
    public DeviceInfo? Info { get; private set; }
    public ushort? ProductId => _transport?.ProductId;

    public IReadOnlyCollection<Section> Pending
    {
        get { lock (_lock) return _pending.Keys.ToArray(); }
    }

    public event Action? StatusChanged;
    public event Action<DpiNotification>? DpiNotified;
    public event Action<string>? SendFailed;
    public event Action<int, int>? Progress;

    public void Submit(Profile profile, params Section[] sections)
    {
        var snapshot = profile.Clone();
        lock (_lock)
        {
            _profile = snapshot;
            foreach (var s in sections)
                _pending[s] = ++_version;
        }
        _wake.Set();
        StatusChanged?.Invoke();
    }

    public void SubmitAll(Profile profile) => Submit(profile, Sections.All);

    public void FactoryReset()
    {
        lock (_lock)
        {
            _resetRequested = true;
            _pending.Clear();
        }
        _wake.Set();
        StatusChanged?.Invoke();
    }

    /// <summary>
    /// Pisca uma cor no LED do mouse (cor, apagado, cor) e volta ao efeito de luz do perfil.
    /// O firmware só mostra a cor do DPI quando o botão do mouse é usado; pelo app isso é feito pela iluminação.
    /// </summary>
    public void Flash(Rgb color) => Flash(color, DateTime.UtcNow);

    internal void Flash(Rgb color, DateTime nowUtc)
    {
        lock (_lock)
            _flash = (color, nowUtc);
        _wake.Set();
    }

    /// <summary>Chamado quando o Windows avisa que um dispositivo entrou/saiu.</summary>
    public void Poke()
    {
        _nextConnect = DateTime.MinValue;
        _wake.Set();
    }

    public void Start()
    {
        _worker = new Thread(WorkerLoop) { IsBackground = true, Name = "M800 worker" };
        _reader = new Thread(ReaderLoop) { IsBackground = true, Name = "M800 notificações" };
        _worker.Start();
        _reader.Start();
    }

    private void WorkerLoop()
    {
        while (!_disposed)
        {
            try
            {
                Tick(DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                _log.Write("erro inesperado: " + ex);
            }
            _wake.WaitOne(TimeSpan.FromMilliseconds(250));
        }
    }

    private void ReaderLoop()
    {
        while (!_disposed)
        {
            var transport = _transport;
            if (transport is null)
            {
                Thread.Sleep(500);
                continue;
            }
            try
            {
                var report = transport.ReadNotification(500);
                if (report is not null && Responses.ParseNotification(report) is { } n)
                {
                    _log.Write($"notificação: nível {n.DpiLevel + 1}, {n.PollingRateHz} Hz");
                    DpiNotified?.Invoke(n);
                }
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException)
            {
                Thread.Sleep(500);
            }
        }
    }

    internal void Tick(DateTime now)
    {
        if (_transport is null)
        {
            if (now < _nextConnect)
                return;
            _nextConnect = now + ReconnectInterval;
            var transport = _factory.TryOpen();
            if (transport is null)
            {
                SetState(ConnectionState.Disconnected, null);
                return;
            }
            _transport = transport;
            _nextInfo = DateTime.MinValue;
            _log.Write($"conectado (PID {transport.ProductId:X4})");
        }

        bool hasWork;
        lock (_lock)
        {
            if (_flash is { } f && now - f.At > FlashMaxAge)
                _flash = null;
            hasWork = _pending.Count > 0 || _resetRequested || _flash is not null;
        }

        if (now >= _nextInfo || (hasWork && now - _lastInfo > MaxInfoAgeBeforeSend))
        {
            if (!RefreshInfo(now))
                return;
        }

        if (hasWork && State == ConnectionState.Online)
            Flush(now);
    }

    private bool RefreshInfo(DateTime now)
    {
        try
        {
            DeviceInfo? info = null;
            for (int attempt = 0; attempt < 3 && info is null; attempt++)
            {
                _transport!.SetFeature(Packets.InfoRequest(_sequence++));
                _sleep(TimeSpan.FromMilliseconds(10));
                info = Responses.ParseInfo(_transport.GetFeature());
            }
            if (info is null)
                throw new IOException("o mouse não respondeu à consulta de estado");

            _lastInfo = now;
            bool hasPending;
            lock (_lock)
                hasPending = _pending.Count > 0;
            _nextInfo = now + (info.Online && !hasPending ? InfoInterval : ShortInfoInterval);
            SetState(info.Online ? ConnectionState.Online : ConnectionState.Sleeping, info);
            return true;
        }
        catch (Exception ex) when (ex is IOException or FormatException or ObjectDisposedException)
        {
            _log.Write("falha na consulta de estado: " + ex.Message);
            Disconnect();
            return false;
        }
    }

    private void Flush(DateTime now)
    {
        Profile? profile;
        List<KeyValuePair<Section, long>> work;
        bool reset;
        (Rgb Color, DateTime At)? flash;
        lock (_lock)
        {
            profile = _profile;
            work = _pending.OrderBy(kv => kv.Key).ToList();
            reset = _resetRequested;
            flash = _flash;
            _flash = null;
        }

        var table = DpiTables.Get(Info?.SensorId ?? DpiTables.Paw3370);
        var batches = new List<(Section? Section, long Version, IReadOnlyList<OutgoingPacket> Packets)>();
        if (reset)
            batches.Add((null, 0, [ProfileEncoder.FactoryReset()]));
        if (profile is not null)
        {
            foreach (var (section, version) in work)
            {
                try
                {
                    batches.Add((section, version, ProfileEncoder.Encode(profile, section, table)));
                }
                catch (ArgumentException ex)
                {
                    RemovePending(section, version);
                    SendFailed?.Invoke($"{Sections.DisplayName(section)}: {ex.Message}");
                }
            }
        }

        int total = batches.Sum(b => b.Packets.Count), sent = 0;
        foreach (var batch in batches)
        {
            foreach (var packet in batch.Packets)
            {
                if (!SendWithRetry(packet))
                {
                    SendFailed?.Invoke(batch.Section is { } s
                        ? $"Não foi possível enviar \"{Sections.DisplayName(s)}\". Será tentado de novo quando o mouse reconectar."
                        : "Não foi possível enviar o reset de fábrica.");
                    Disconnect();
                    return;
                }
                Progress?.Invoke(++sent, total);
            }
            if (batch.Section is { } done)
                RemovePending(done, batch.Version);
            else
                lock (_lock) _resetRequested = false;
        }
        if (flash is { } f && now - f.At <= FlashMaxAge && profile is not null)
            BlinkAndRestore(f.Color, profile, table);
        StatusChanged?.Invoke();
    }

    private void BlinkAndRestore(Rgb color, Profile profile, SensorDpiTable table)
    {
        var on = new OutgoingPacket(Command.Lighting, Payloads.Lighting(LightingMode.Steady, 4, 1, [color], 0));
        var off = new OutgoingPacket(Command.Lighting, Payloads.Lighting(LightingMode.Off, 4, 1, [], 0));
        var steps = new (OutgoingPacket Packet, int HoldMs)[] { (on, 350), (off, 150), (on, 350) };
        foreach (var (packet, holdMs) in steps)
        {
            if (!SendWithRetry(packet))
                return;
            _sleep(TimeSpan.FromMilliseconds(holdMs));
        }
        foreach (var packet in ProfileEncoder.Encode(profile, Section.Lighting, table))
            SendWithRetry(packet);
    }

    private void RemovePending(Section section, long version)
    {
        lock (_lock)
        {
            if (_pending.TryGetValue(section, out var current) && current == version)
                _pending.Remove(section);
        }
    }

    private bool SendWithRetry(OutgoingPacket packet)
    {
        var report = Packets.Build(packet.Command, packet.Payload, _sequence++, packet.Chunk);
        for (int attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                _transport!.SetFeature(report);
                _log.Write("enviado " + Convert.ToHexString(report));
                _sleep(ProtocolConstants.InterPacketDelay);
                return true;
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException)
            {
                _log.Write($"falha no envio (tentativa {attempt}): {ex.Message}");
                _sleep(TimeSpan.FromMilliseconds(100));
            }
        }
        return false;
    }

    private void Disconnect()
    {
        var transport = _transport;
        _transport = null;
        _nextConnect = DateTime.MinValue;
        transport?.Dispose();
        SetState(ConnectionState.Disconnected, null);
    }

    private void SetState(ConnectionState state, DeviceInfo? info)
    {
        bool changed = state != State || info != Info;
        State = state;
        Info = info;
        if (changed)
            StatusChanged?.Invoke();
    }

    public void Dispose()
    {
        _disposed = true;
        _wake.Set();
        _worker?.Join(TimeSpan.FromSeconds(2));
        _reader?.Join(TimeSpan.FromSeconds(2));
        _transport?.Dispose();
        _transport = null;
    }
}
