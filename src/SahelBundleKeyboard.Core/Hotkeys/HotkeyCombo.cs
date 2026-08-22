namespace SahelBundleKeyboard.Core.Hotkeys;

[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Ctrl = 1,
    Alt = 2,
    Shift = 4,
    Win = 8
}

/// <summary>
/// A parsed global shortcut. Immutable value object; equality compares modifiers + key.
/// </summary>
public sealed class HotkeyCombo : IEquatable<HotkeyCombo>
{
    public HotkeyModifiers Modifiers { get; }

    /// <summary>Win32 virtual-key code.</summary>
    public int VirtualKey { get; }

    /// <summary>Canonical display form, e.g. "Ctrl+Alt+G".</summary>
    public string Canonical { get; }

    public HotkeyCombo(HotkeyModifiers modifiers, int virtualKey, string canonical)
    {
        Modifiers = modifiers;
        VirtualKey = virtualKey;
        Canonical = canonical;
    }

    public bool Equals(HotkeyCombo? other)
    {
        if (other is null)
        {
            return false;
        }

        return Modifiers == other.Modifiers && VirtualKey == other.VirtualKey;
    }

    public override bool Equals(object? obj) => Equals(obj as HotkeyCombo);

    public override int GetHashCode() => (int)Modifiers * 397 ^ VirtualKey;

    public override string ToString() => Canonical;
}
