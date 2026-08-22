using System.ComponentModel;
using System.Runtime.InteropServices;
using SahelBundleKeyboard.Core.Automation;
using SahelBundleKeyboard.Windows.Native;

namespace SahelBundleKeyboard.Windows.Input;

/// <summary>
/// Injects keystrokes through SendInput. Text is sent as Unicode keyboard events
/// (works for Arabic and English regardless of layout); Enter uses the virtual key.
/// Surrogate pairs are sent inside one SendInput batch so they never split.
/// </summary>
public sealed class SendInputKeystrokeSender : IKeystrokeSender
{
    /// <summary>Small pause between characters so slow message pumps keep up.</summary>
    private const int InterCharacterDelayMs = 1;

    private static readonly int CbSize = Marshal.SizeOf<NativeMethods.INPUT>();

    public void TypeText(string text)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);

        var pending = new List<char>(4);

        Span<char> sequence = stackalloc char[2];

        foreach (var rune in text.EnumerateRunes())
        {
            var length = rune.EncodeToUtf16(sequence);
            pending.Clear();
            pending.AddRange(sequence[..length]);
            SendUnits(pending);
        }
    }

    public void PressEnter()
    {
        var inputs = new NativeMethods.INPUT[2];

        inputs[0].type = NativeMethods.INPUT_KEYBOARD;
        inputs[0].ki.wVk = NativeMethods.VK_RETURN;
        inputs[0].ki.wScan = 0x1C; // Enter scan code for apps that prefer scancodes
        inputs[0].ki.dwFlags = 0;

        inputs[1].type = NativeMethods.INPUT_KEYBOARD;
        inputs[1].ki.wVk = NativeMethods.VK_RETURN;
        inputs[1].ki.wScan = 0x1C;
        inputs[1].ki.dwFlags = NativeMethods.KEYEVENTF_KEYUP;

        SendBatch(inputs);
    }

    private static void SendUnits(List<char> units)
    {
        var inputs = new NativeMethods.INPUT[units.Count * 2];

        for (var i = 0; i < units.Count; i++)
        {
            var downIndex = i * 2;
            inputs[downIndex].type = NativeMethods.INPUT_KEYBOARD;
            inputs[downIndex].ki.wVk = 0;
            inputs[downIndex].ki.wScan = units[i];
            inputs[downIndex].ki.dwFlags = NativeMethods.KEYEVENTF_UNICODE;

            inputs[downIndex + 1].type = NativeMethods.INPUT_KEYBOARD;
            inputs[downIndex + 1].ki.wVk = 0;
            inputs[downIndex + 1].ki.wScan = units[i];
            inputs[downIndex + 1].ki.dwFlags = NativeMethods.KEYEVENTF_UNICODE | NativeMethods.KEYEVENTF_KEYUP;
        }

        SendBatch(inputs);
        Thread.Sleep(InterCharacterDelayMs);
    }

    private static void SendBatch(NativeMethods.INPUT[] inputs)
    {
        var sent = NativeMethods.SendInput(inputs.Length, inputs, CbSize);
        if (sent != inputs.Length)
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "فشل إرسال ضغطات لوحة المفاتيح عبر SendInput.");
        }
    }
}
