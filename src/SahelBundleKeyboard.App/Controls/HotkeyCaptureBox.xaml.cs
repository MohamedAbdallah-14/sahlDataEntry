using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SahelBundleKeyboard.App.Controls;

/// <summary>
/// Read-only box that captures a global-shortcut keystroke combination and shows it
/// canonically (e.g. Ctrl+Alt+G). Only combos containing Ctrl or Alt are accepted;
/// F12 is rejected because Windows reserves it.
/// </summary>
public partial class HotkeyCaptureBox : UserControl
{
    public static readonly DependencyProperty ShortcutProperty = DependencyProperty.Register(
        nameof(Shortcut), typeof(string), typeof(HotkeyCaptureBox),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public HotkeyCaptureBox()
    {
        InitializeComponent();
        Box.PreviewKeyDown += OnBoxKeyDown;
    }

    public string Shortcut
    {
        get => (string)GetValue(ShortcutProperty);
        set => SetValue(ShortcutProperty, value);
    }

    private void OnBoxKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or
            Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin or Key.Escape)
        {
            return; // modifier-only presses do not change the value
        }

        if (key is Key.Back or Key.Delete &&
            (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt)) == 0)
        {
            Shortcut = string.Empty; // clear the shortcut
            return;
        }

        if (!TryGetToken(key, out var token))
        {
            return;
        }

        var modifiers = Keyboard.Modifiers;
        var parts = new List<string>(4);

        if ((modifiers & ModifierKeys.Control) != 0)
        {
            parts.Add("Ctrl");
        }

        if ((modifiers & ModifierKeys.Alt) != 0)
        {
            parts.Add("Alt");
        }

        if ((modifiers & ModifierKeys.Shift) != 0)
        {
            parts.Add("Shift");
        }

        if ((modifiers & ModifierKeys.Windows) != 0)
        {
            parts.Add("Win");
        }

        parts.Add(token);
        var candidate = string.Join("+", parts);

        if (Core.Hotkeys.HotkeyParser.TryParse(candidate) is not null)
        {
            Shortcut = candidate;
        }
        else
        {
            Shortcut = Shortcut; // keep previous value on invalid combos
        }
    }

    private static bool TryGetToken(Key key, out string token)
    {
        token = key switch
        {
            >= Key.A and <= Key.Z => key.ToString(),
            >= Key.D0 and <= Key.D9 => ((int)(key - Key.D0)).ToString(),
            >= Key.NumPad0 and <= Key.NumPad9 => "NumPad" + ((int)(key - Key.NumPad0)),
            >= Key.F1 and <= Key.F11 => key.ToString(),
            Key.Insert => "Insert",
            Key.Delete => "Delete",
            Key.Home => "Home",
            Key.End => "End",
            Key.PageUp => "PageUp",
            Key.PageDown => "PageDown",
            _ => string.Empty
        };

        return token.Length > 0;
    }
}
