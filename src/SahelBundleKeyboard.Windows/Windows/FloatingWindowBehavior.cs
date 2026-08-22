using System.Runtime.InteropServices;
using SahelBundleKeyboard.Windows.Native;

namespace SahelBundleKeyboard.Windows.Windows;

/// <summary>
/// Win32 window styling helpers for the non-activating floating controller:
/// WS_EX_NOACTIVATE + WS_EX_TOOLWINDOW + WS_EX_TOPMOST and WM_MOUSEACTIVATE
/// returning MA_NOACTIVATE so clicking never steals keyboard focus.
/// </summary>
public static class FloatingWindowBehavior
{
    /// <summary>Applies the no-activate ex-style. Call after the HWND exists.</summary>
    public static void ApplyNoActivateStyle(IntPtr hwnd)
    {
        var exStyle = NativeMethods.GetWindowLongW(hwnd, NativeMethods.GWL_EXSTYLE);
        _ = NativeMethods.SetWindowLongW(
            hwnd,
            NativeMethods.GWL_EXSTYLE,
            exStyle | NativeMethods.WS_EX_NOACTIVATE |
            NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_TOPMOST);
    }

    /// <summary>WndProc fragment: return MA_NOACTIVATE for WM_MOUSEACTIVATE.</summary>
    public static IntPtr? HandleMessage(uint msg)
    {
        if (msg == NativeMethods.WM_MOUSEACTIVATE)
        {
            return new IntPtr(NativeMethods.MA_NOACTIVATE);
        }

        return null;
    }
}

public static class WindowLongExtensions
{
    public static int GetExStyle(IntPtr hwnd) =>
        NativeMethods.GetWindowLongW(hwnd, NativeMethods.GWL_EXSTYLE);
}
