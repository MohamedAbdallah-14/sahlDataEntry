using SahelBundleKeyboard.Core.Hotkeys;

namespace SahelBundleKeyboard.Core.Tests.Hotkeys;

public class HotkeyParserTests
{
    [Theory]
    [InlineData("Ctrl+Alt+G", "Ctrl+Alt+G")]
    [InlineData("Control + Alt + G", "Ctrl+Alt+G")]
    [InlineData("ctrl+alt+p", "Ctrl+Alt+P")]
    [InlineData("Alt+Shift+F5", "Alt+Shift+F5")]
    [InlineData("Ctrl+Shift+NumPad7", "Ctrl+Shift+NumPad7")]
    public void Parse_NormalizesToCanonicalForm(string input, string expectedCanonical)
    {
        var combo = HotkeyParser.TryParse(input);
        Assert.NotNull(combo);
        Assert.Equal(expectedCanonical, combo!.Canonical);
    }

    [Theory]
    [InlineData("G")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("Ctrl+")]
    [InlineData("Ctrl+G+Alt")]
    [InlineData("NotAModifier+G")]
    [InlineData("Shift+F1")] // no Ctrl/Alt: would hijack normal typing
    [InlineData("Win+N")]
    [InlineData("F12")]
    [InlineData("Ctrl+F12")]
    [InlineData("Ctrl+@")]
    public void Parse_RejectsInvalidOrUnsafeCombos(string? input)
    {
        Assert.Null(HotkeyParser.TryParse(input));
    }

    [Fact]
    public void Parse_ResolvesVirtualKeyCodes()
    {
        var g = HotkeyParser.TryParse("Ctrl+Alt+G")!;
        Assert.Equal(0x47, g.VirtualKey);
        Assert.Contains(HotkeyModifiers.Ctrl, new[] { g.Modifiers & HotkeyModifiers.Ctrl });

        var f5 = HotkeyParser.TryParse("Ctrl+F5")!;
        Assert.Equal(0x74, f5.VirtualKey); // VK_F5

        var digit = HotkeyParser.TryParse("Alt+1")!;
        Assert.Equal(0x31, digit.VirtualKey);
    }

    [Fact]
    public void Equality_IgnoresDisplayOrderButMatchesModifiersAndKey()
    {
        var a = HotkeyParser.TryParse("Ctrl+Alt+P")!;
        var b = HotkeyParser.TryParse("control+alt+p")!;
        Assert.Equal(a, b);
        Assert.NotEqual(a, HotkeyParser.TryParse("Ctrl+Alt+S")!);
        Assert.NotEqual(a, HotkeyParser.TryParse("Ctrl+Shift+P")!);
    }

    [Theory]
    [InlineData("Ctrl+F12", false)]
    [InlineData("Ctrl+Alt+B", true)]
    [InlineData("", false)]
    [InlineData("X+Z", false)]
    public void TryValidate_ReportsErrors(string input, bool expectedValid)
    {
        var ok = HotkeyParser.TryValidate(input, out var error);
        Assert.Equal(expectedValid, ok);
        if (!expectedValid)
        {
            Assert.False(string.IsNullOrWhiteSpace(error));
        }
    }

    [Fact]
    public void F12_IsRejectedWithReservedKeyMessage()
    {
        HotkeyParser.TryValidate("Ctrl+Alt+F12", out _);
        HotkeyParser.TryValidate("Ctrl+F12", out var error);
        Assert.Equal(HotkeyParser.ReservedKeyError, error);

        HotkeyParser.TryValidate("Ctrl+Alt+F12", out var error2);
        Assert.Equal(HotkeyParser.ReservedKeyError, error2);
    }
}
