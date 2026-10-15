using System.ComponentModel;
using System.Runtime.InteropServices;

namespace M800.App.Platform;

/// <summary>Captura (e bloqueia) as teclas enquanto a janela dona estiver em primeiro plano.
/// Usado sÃ³ durante a gravaÃ§Ã£o de macro; com outra janela ativa (ou o app escondido) as teclas passam normalmente.</summary>
public sealed class LowLevelKeyboardHook : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100, WM_KEYUP = 0x0101, WM_SYSKEYDOWN = 0x0104, WM_SYSKEYUP = 0x0105;

    private delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public int vkCode;
        public int scanCode;
        public int flags;
        public int time;
        public IntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, HookProc callback, IntPtr module, uint threadId);

    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? moduleName);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    private readonly HookProc _callback; // mantÃ©m o delegate vivo
    private readonly IntPtr _owner;
    private IntPtr _hook;

    public LowLevelKeyboardHook(IntPtr owner)
    {
        _owner = owner;
        _callback = Callback;
        _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _callback, GetModuleHandle(null), 0);
        if (_hook == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "nÃ£o foi possÃ­vel capturar o teclado");
    }

    /// <summary>(tecla virtual, soltou?, horÃ¡rio em ms)</summary>
    public event Action<int, bool, long>? KeyEvent;

    private IntPtr Callback(int code, IntPtr wParam, IntPtr lParam)
    {
        int message = (int)wParam;
        if (code >= 0 && message is (WM_KEYDOWN or WM_SYSKEYDOWN or WM_KEYUP or WM_SYSKEYUP) && GetForegroundWindow() == _owner)
        {
            var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            KeyEvent?.Invoke(data.vkCode, message is WM_KEYUP or WM_SYSKEYUP, Environment.TickCount64);
            return 1; // bloqueia: a tecla nÃ£o chega ao Windows durante a gravaÃ§Ã£o
        }
        return CallNextHookEx(_hook, code, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }
    }
}
