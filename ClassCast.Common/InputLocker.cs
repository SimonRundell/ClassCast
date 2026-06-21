using System.Diagnostics;
using System.Runtime.InteropServices;
using ClassCast.Common.Logging;

namespace ClassCast.Common;

/// <summary>
/// Blocks all keyboard and mouse input system-wide using low-level Windows hooks
/// (<c>WH_KEYBOARD_LL</c> and <c>WH_MOUSE_LL</c>). While locked, the hook
/// procedures return <c>1</c> to swallow every input event; while unlocked, events
/// are passed through via <c>CallNextHookEx</c>.
/// </summary>
/// <remarks>
/// Low-level hooks require their procedures to be serviced by a message loop on the
/// thread that installed them (specification section 5.4). This class therefore owns
/// a dedicated background thread that installs the hooks and runs a manual message
/// pump for their lifetime.
/// </remarks>
public sealed class InputLocker : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WH_MOUSE_LL = 14;
    private const int HC_ACTION = 0;
    private const uint WM_QUIT = 0x0012;

    private readonly LowLevelProc _keyboardProc;
    private readonly LowLevelProc _mouseProc;

    private nint _keyboardHook;
    private nint _mouseHook;
    private Thread? _pumpThread;
    private uint _pumpThreadId;
    private readonly ManualResetEventSlim _installed = new(false);
    private volatile bool _locked;
    private volatile bool _disposed;

    /// <summary>Initialises the locker. Call <see cref="Start"/> to install the hooks.</summary>
    public InputLocker()
    {
        // Hold the delegates in fields so the GC cannot collect them while the
        // unmanaged hook holds a pointer to them.
        _keyboardProc = KeyboardHookProc;
        _mouseProc = MouseHookProc;
    }

    /// <summary>Gets a value indicating whether input is currently being blocked.</summary>
    public bool IsLocked => _locked;

    /// <summary>
    /// Installs the keyboard and mouse hooks on a dedicated message-pump thread.
    /// Hooks are installed in the unlocked (pass-through) state.
    /// </summary>
    public void Start()
    {
        if (_pumpThread is not null)
        {
            return;
        }

        _pumpThread = new Thread(PumpThreadMain)
        {
            IsBackground = true,
            Name = "ClassCast-InputLocker"
        };
        _pumpThread.SetApartmentState(ApartmentState.STA);
        _pumpThread.Start();

        // Wait until the hooks are actually installed before returning.
        _installed.Wait(TimeSpan.FromSeconds(5));
    }

    /// <summary>Begins blocking keyboard and mouse input.</summary>
    public void Lock() => _locked = true;

    /// <summary>Stops blocking keyboard and mouse input (hooks remain installed).</summary>
    public void Unlock() => _locked = false;

    /// <summary>Sets the lock state explicitly.</summary>
    /// <param name="locked"><c>true</c> to block input; <c>false</c> to allow it.</param>
    public void SetLocked(bool locked) => _locked = locked;

    /// <summary>Thread entry point: installs hooks, runs the message pump, then unhooks.</summary>
    private void PumpThreadMain()
    {
        _pumpThreadId = GetCurrentThreadId();

        nint hModule;
        using (Process current = Process.GetCurrentProcess())
        using (ProcessModule? module = current.MainModule)
        {
            hModule = module is not null ? GetModuleHandle(module.ModuleName) : nint.Zero;
        }

        _keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, hModule, 0);
        _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, hModule, 0);

        if (_keyboardHook == nint.Zero || _mouseHook == nint.Zero)
        {
            Logger.Error($"Failed to install input hooks (kbd={_keyboardHook}, mouse={_mouseHook}, err={Marshal.GetLastWin32Error()}).");
        }
        else
        {
            Logger.Info("Input hooks installed.");
        }

        _installed.Set();

        // Manual message pump keeps the low-level hooks alive on this thread.
        while (GetMessage(out MSG msg, nint.Zero, 0, 0) > 0)
        {
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }

        if (_keyboardHook != nint.Zero)
        {
            UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = nint.Zero;
        }
        if (_mouseHook != nint.Zero)
        {
            UnhookWindowsHookEx(_mouseHook);
            _mouseHook = nint.Zero;
        }
        Logger.Info("Input hooks removed.");
    }

    /// <summary>Keyboard hook callback. Swallows input while locked.</summary>
    private nint KeyboardHookProc(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= HC_ACTION && _locked)
        {
            return 1; // Block the keystroke.
        }
        return CallNextHookEx(nint.Zero, nCode, wParam, lParam);
    }

    /// <summary>Mouse hook callback. Swallows input while locked.</summary>
    private nint MouseHookProc(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= HC_ACTION && _locked)
        {
            return 1; // Block the mouse event.
        }
        return CallNextHookEx(nint.Zero, nCode, wParam, lParam);
    }

    /// <summary>Removes the hooks and stops the message-pump thread.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _locked = false;

        if (_pumpThread is not null && _pumpThreadId != 0)
        {
            // Ask the pump thread to exit its GetMessage loop.
            PostThreadMessage(_pumpThreadId, WM_QUIT, nint.Zero, nint.Zero);
            _pumpThread.Join(TimeSpan.FromSeconds(2));
            _pumpThread = null;
        }

        _installed.Dispose();
    }

    // ----- P/Invoke declarations ------------------------------------------

    private delegate nint LowLevelProc(int nCode, nint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public nint hwnd;
        public uint message;
        public nint wParam;
        public nint lParam;
        public uint time;
        public POINT pt;
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern nint SetWindowsHookEx(int idHook, LowLevelProc lpfn, nint hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(nint hhk);

    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern nint GetModuleHandle(string lpModuleName);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern int GetMessage(out MSG lpMsg, nint hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern nint DispatchMessage(ref MSG lpMsg);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostThreadMessage(uint idThread, uint Msg, nint wParam, nint lParam);
}
