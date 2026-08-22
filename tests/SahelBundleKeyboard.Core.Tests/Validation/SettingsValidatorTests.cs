using SahelBundleKeyboard.Core.Models;
using SahelBundleKeyboard.Core.Validation;

namespace SahelBundleKeyboard.Core.Tests.Validation;

public class SettingsValidatorTests
{
    [Fact]
    public void DefaultSettings_Pass()
    {
        var result = SettingsValidator.Validate(new AppSettings());
        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.Message)));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(5001)]
    public void DelayOutsideRange_Fails(int delayMs)
    {
        var settings = new AppSettings { DelayMilliseconds = delayMs };
        Assert.False(SettingsValidator.Validate(settings).IsValid);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    public void CountdownOutsideZeroToThree_Fails(int countdown)
    {
        var settings = new AppSettings { CountdownSeconds = countdown };
        Assert.False(SettingsValidator.Validate(settings).IsValid);
    }

    [Fact]
    public void DuplicateShortcuts_AreReportedAsConflict()
    {
        var settings = new AppSettings
        {
            StartShortcut = "Ctrl+Alt+X",
            StopShortcut = "Ctrl+Alt+X"
        };

        var result = SettingsValidator.Validate(settings);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "Shortcuts");
    }

    [Fact]
    public void ReservedOrMalformedShortcut_IsRejected()
    {
        var settings = new AppSettings { StartShortcut = "Ctrl+F12" };
        var result = SettingsValidator.Validate(settings);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "Start");
    }
}
