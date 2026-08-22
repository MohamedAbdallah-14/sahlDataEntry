using System.ComponentModel;
using System.Runtime.InteropServices;
using SahelBundleKeyboard.Core.Hotkeys;
using SahelBundleKeyboard.Windows.Native;

namespace SahelBundleKeyboard.Windows.Hotkeys;

public sealed class HotkeyRegistrationException : Exception
{
    public string UserMessage { get; }

    public HotkeyRegistrationException(string userMessage, int win32Error)
        : base($"{userMessage} (Win32 error {win32Error})")
    {
        UserMessage = userMessage;
    }
}

/// <summary>
/// Registers/unregisters system-wide hotkeys against a message-only window.
/// Applies MOD_NOREPEAT. On any registration conflict the whole set rolls back
/// to the previous valid configuration so the app never ends up half-registered.
/// </summary>
public sealed class GlobalHotkeyManager : IDisposable
{
    private readonly MessageOnlyWindow _window;
    private readonly Dictionary<int, HotkeyEntry> _registered = [];
    private bool _disposed;

    /// <summary>Receives the action id (e.g. "Start") on the thread that owns the window.</summary>
    public event Action<string>? HotkeyPressed;

    public GlobalHotkeyManager()
    {
        _window = new MessageOnlyWindow();
        _window.HotkeyReceived += id =>
        {
            if (_registered.TryGetValue(id, out var entry))
            {
                HotkeyPressed?.Invoke(entry.ActionId);
            }
        };
    }

    /// <summary>
    /// Replaces the active shortcut set. If any new combo fails to register,
    /// everything is unregistered and the previous set is restored before throwing.
    /// </summary>
    public void Apply(IReadOnlyList<HotkeyEntry> shortcuts)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(shortcuts);

        if (shortcuts.Select(s => s.Id).Distinct().Count() != shortcuts.Count ||
            shortcuts.Select(s => s.Combo).Distinct().Count() != shortcuts.Count)
        {
            throw new HotkeyRegistrationException(
                "يوجد تعارض: لا يمكن ربط نفس الاختصار أو نفس الرقم الداخلي بوظيفتين مختلفتين.", 0);
        }

        var previous = _registered.Values.ToArray();

        UnregisterAll();

        try
        {
            foreach (var shortcut in shortcuts)
            {
                var mods = ToNativeModifiers(shortcut.Combo.Modifiers) | NativeMethods.MOD_NOREPEAT;
                var ok = NativeMethods.RegisterHotKey(
                    _window.Handle, shortcut.Id, mods, (uint)shortcut.Combo.VirtualKey);

                if (!ok)
                {
                    var error = Marshal.GetLastWin32Error();
                    throw new HotkeyRegistrationException(
                        $"تعذر تسجيل اختصار {shortcut.Combo.Canonical}. قد يكون مستخدماً من برنامج آخر.", error);
                }

                _registered[shortcut.Id] = shortcut;
            }
        }
        catch (HotkeyRegistrationException)
        {
            UnregisterAll();
            Restore(previous);
            throw;
        }
    }

    private void Restore(HotkeyEntry[] previous)
    {
        foreach (var entry in previous)
        {
            var mods = ToNativeModifiers(entry.Combo.Modifiers) | NativeMethods.MOD_NOREPEAT;
            if (NativeMethods.RegisterHotKey(_window.Handle, entry.Id, mods, (uint)entry.Combo.VirtualKey))
            {
                _registered[entry.Id] = entry;
            }
        }
    }

    private void UnregisterAll()
    {
        foreach (var id in _registered.Keys.ToArray())
        {
            _ = NativeMethods.UnregisterHotKey(_window.Handle, id);
            _ = _registered.Remove(id);
        }
    }

    internal static uint ToNativeModifiers(HotkeyModifiers modifiers)
    {
        var result = 0u;
        if ((modifiers & HotkeyModifiers.Ctrl) != 0)
        {
            result |= NativeMethods.MOD_CONTROL;
        }

        if ((modifiers & HotkeyModifiers.Alt) != 0)
        {
            result |= NativeMethods.MOD_ALT;
        }

        if ((modifiers & HotkeyModifiers.Shift) != 0)
        {
            result |= NativeMethods.MOD_SHIFT;
        }

        if ((modifiers & HotkeyModifiers.Win) != 0)
        {
            result |= NativeMethods.MOD_WIN;
        }

        return result;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        UnregisterAll();
        _window.Dispose();
    }
}

public sealed record HotkeyEntry(int Id, string ActionId, HotkeyCombo Combo);
