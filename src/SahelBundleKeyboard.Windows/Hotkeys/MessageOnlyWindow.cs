using System.Runtime.InteropServices;
using SahelBundleKeyboard.Windows.Native;

namespace SahelBundleKeyboard.Windows.Hotkeys;

/// <summary>
/// Message-only window (HWND_MESSAGE) that receives WM_HOTKEY for registered
/// system-wide shortcuts. Runs a minimal Win32 window; no WPF dependency.
/// </summary>
public sealed class MessageOnlyWindow : IDisposable
{
    private const string ClassName = "SahelBundleKeyboard_MsgOnly";

    private delegate IntPtr WndProcHandler(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private readonly IntPtr _handle;
    private readonly WndProcHandler _wndProc;
    private readonly IntPtr _hInstance;
    private readonly bool _registeredClass;
    private bool _disposed;

    public event Action<int>? HotkeyReceived;

    public IntPtr Handle => _handle;

    public MessageOnlyWindow()
    {
        _wndProc = WndProc;
        _hInstance = NativeMethods.GetModuleHandleW(null);

        var wc = new NativeMethods.WNDCLASS
        {
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
            hInstance = _hInstance,
            lpszClassName = ClassName
        };

        var atom = NativeMethods.RegisterClassW(ref wc);
        if (atom == 0 && Marshal.GetLastWin32Error() != 1410) // 1410 = class already registered
        {
            throw new System.ComponentModel.Win32Exception(
                Marshal.GetLastWin32Error(),
                $"تعذر تسجيل نافذة الرسائل الداخلية (RegisterClass، رمز الخطأ {Marshal.GetLastWin32Error()}).");
        }

        _registeredClass = true;

        _handle = NativeMethods.CreateWindowExW(
            0, ClassName, ClassName, 0,
            0, 0, 0, 0,
            NativeMethods.HWND_MESSAGE,
            IntPtr.Zero, _hInstance, IntPtr.Zero);

        if (_handle == IntPtr.Zero)
        {
            var createError = Marshal.GetLastWin32Error();
            throw new System.ComponentModel.Win32Exception(
                createError,
                $"تعذر إنشاء نافذة الرسائل الداخلية (CreateWindow، رمز الخطأ {createError}).");
        }
    }

    /// <summary>Pumps Windows messages on the calling thread. Blocks until the window is destroyed.</summary>
    public void RunMessageLoop()
    {
        // GetMessage returns 0 on WM_QUIT and -1 on error; both end the loop.
        while (NativeMethods.GetMessageW(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            _ = NativeMethods.TranslateMessage(ref msg);
            _ = NativeMethods.DispatchMessageW(ref msg);
        }
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == NativeMethods.WM_HOTKEY)
        {
            HotkeyReceived?.Invoke(wParam.ToInt32());
        }

        return NativeMethods.DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_handle != IntPtr.Zero)
        {
            // In the WPF host the dispatcher pumps this thread's messages, so WM_HOTKEY
            // arrives there; RunMessageLoop exists only for non-WPF hosts/tests.
            _ = NativeMethods.DestroyWindow(_handle);
        }

        if (_registeredClass)
        {
            _ = NativeMethods.UnregisterClassW(ClassName, _hInstance);
        }
    }
}
