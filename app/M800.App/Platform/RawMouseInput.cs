using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;

namespace M800.App.Platform;

/// <param name="TimeMs">horário de chegada (relógio de alta precisão), em ms.</param>
public readonly record struct RawMouseReport(IntPtr Device, ushort ButtonFlags, int DeltaX, int DeltaY, double TimeMs);

/// <summary>
/// Recebe cada relatório bruto dos mouses (Raw Input do Windows) enquanto a janela está em primeiro plano.
/// Usado pelos testes de polling e de cliques; não interfere no uso normal do mouse.
/// </summary>
public sealed class RawMouseInput : IDisposable
{
    public const ushort LeftButtonDown = 0x0001, LeftButtonUp = 0x0002;

    private const int WM_INPUT = 0x00FF;
    private const uint RID_INPUT = 0x10000003, RIDI_DEVICENAME = 0x20000007, RIM_TYPEMOUSE = 0, RIDEV_REMOVE = 0x01;

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUTDEVICE
    {
        public ushort UsagePage;
        public ushort Usage;
        public uint Flags;
        public IntPtr Target;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUTHEADER
    {
        public uint Type;
        public uint Size;
        public IntPtr Device;
        public IntPtr WParam;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] devices, uint count, uint size);

    [DllImport("user32.dll")]
    private static extern uint GetRawInputData(IntPtr rawInput, uint command, IntPtr data, ref uint size, uint headerSize);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint GetRawInputDeviceInfo(IntPtr device, uint command, StringBuilder? data, ref uint size);

    private readonly HwndSource _source;
    private readonly Dictionary<IntPtr, bool> _isM800 = [];
    private readonly uint _headerSize = (uint)Marshal.SizeOf<RAWINPUTHEADER>();
    private IntPtr _buffer = Marshal.AllocHGlobal(128);
    private uint _bufferSize = 128;

    public RawMouseInput(Window window)
    {
        var hwnd = new WindowInteropHelper(window).EnsureHandle();
        _source = HwndSource.FromHwnd(hwnd);
        var devices = new[] { new RAWINPUTDEVICE { UsagePage = 0x01, Usage = 0x02, Flags = 0, Target = hwnd } };
        if (!RegisterRawInputDevices(devices, 1, (uint)Marshal.SizeOf<RAWINPUTDEVICE>()))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "não foi possível ler o mouse");
        _source.AddHook(WndProc);
    }

    public event Action<RawMouseReport>? Report;

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WM_INPUT)
            return IntPtr.Zero;
        double now = Stopwatch.GetTimestamp() * 1000.0 / Stopwatch.Frequency;
        uint size = 0;
        GetRawInputData(lParam, RID_INPUT, IntPtr.Zero, ref size, _headerSize);
        if (size > _bufferSize)
        {
            Marshal.FreeHGlobal(_buffer);
            _buffer = Marshal.AllocHGlobal((int)size);
            _bufferSize = size;
        }
        if (GetRawInputData(lParam, RID_INPUT, _buffer, ref size, _headerSize) == uint.MaxValue)
            return IntPtr.Zero;
        var header = Marshal.PtrToStructure<RAWINPUTHEADER>(_buffer);
        if (header.Type != RIM_TYPEMOUSE)
            return IntPtr.Zero;
        // RAWMOUSE: usFlags(2) + 2 de alinhamento, usButtonFlags(2), usButtonData(2), ulRawButtons(4), lLastX(4), lLastY(4)
        int o = (int)_headerSize;
        var flags = (ushort)Marshal.ReadInt16(_buffer, o + 4);
        int dx = Marshal.ReadInt32(_buffer, o + 12);
        int dy = Marshal.ReadInt32(_buffer, o + 16);
        Report?.Invoke(new RawMouseReport(header.Device, flags, dx, dy, now));
        return IntPtr.Zero; // não marca como tratado: o Windows segue processando normalmente
    }

    /// <summary>O relatório veio do M800 (VID 248A)? Outros mouses são ignorados nos testes.</summary>
    public bool IsM800(IntPtr device)
    {
        if (_isM800.TryGetValue(device, out bool known))
            return known;
        uint length = 0;
        GetRawInputDeviceInfo(device, RIDI_DEVICENAME, null, ref length);
        var name = new StringBuilder((int)length + 1);
        GetRawInputDeviceInfo(device, RIDI_DEVICENAME, name, ref length);
        bool isM800 = name.ToString().Contains("VID_248A", StringComparison.OrdinalIgnoreCase);
        _isM800[device] = isM800;
        return isM800;
    }

    public void Dispose()
    {
        _source.RemoveHook(WndProc);
        var remove = new[] { new RAWINPUTDEVICE { UsagePage = 0x01, Usage = 0x02, Flags = RIDEV_REMOVE, Target = IntPtr.Zero } };
        RegisterRawInputDevices(remove, 1, (uint)Marshal.SizeOf<RAWINPUTDEVICE>());
        Marshal.FreeHGlobal(_buffer);
        _buffer = IntPtr.Zero;
    }
}
