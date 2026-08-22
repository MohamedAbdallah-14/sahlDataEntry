using SahelBundleKeyboard.Core.Models;
using SahelBundleKeyboard.Core.Validation;

namespace SahelBundleKeyboard.Core.Tests.Validation;

public class BundleValidatorTests
{
    private static Bundle ValidBundle(int items = 1)
    {
        var bundle = new Bundle { Name = "عرض" };
        for (var i = 0; i < items; i++)
        {
            bundle.Items.Add(new BundleItem { ProductName = "صنف " + (i + 1), BaseQuantity = 2, Order = i });
        }

        return bundle;
    }

    [Fact]
    public void ValidBundleAndCount_Passes()
    {
        var result = BundleValidator.ValidateForRun(ValidBundle(), 5);
        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.Message)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void NonPositiveCount_FailsWithArabicMessage(int count)
    {
        var result = BundleValidator.ValidateForRun(ValidBundle(), count);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "bundleCount");
        Assert.All(result.Errors.Select(e => e.Message), m => Assert.NotEmpty(m));
    }

    [Fact]
    public void NullOrEmptyBundle_Fails()
    {
        Assert.False(BundleValidator.ValidateForRun(null, 1).IsValid);
        Assert.False(BundleValidator.ValidateForRun(new Bundle { Name = "فارغة" }, 1).IsValid);
    }

    [Fact]
    public void ItemWithoutName_Fails()
    {
        var bundle = new Bundle { Name = "ب" };
        bundle.Items.Add(new BundleItem { ProductName = "  ", BaseQuantity = 1 });

        var result = BundleValidator.ValidateForRun(bundle, 1);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == nameof(BundleItem.ProductName));
    }

    [Fact]
    public void ZeroQuantity_Fails_NegativePriceFails()
    {
        var bundle = new Bundle { Name = "ب" };
        bundle.Items.Add(new BundleItem { ProductName = "س", BaseQuantity = 0 });
        bundle.Items.Add(new BundleItem { ProductName = "ع", BaseQuantity = 1, CustomPrice = -1 });

        var result = BundleValidator.ValidateForRun(bundle, 1);

        Assert.Contains(result.Errors, e => e.Field == nameof(BundleItem.BaseQuantity));
        Assert.Contains(result.Errors, e => e.Field == nameof(BundleItem.CustomPrice));
    }

    [Fact]
    public void MissingBundleName_FailsButItemsStillValidated()
    {
        var bundle = new Bundle { Name = "" };
        bundle.Items.Add(new BundleItem { ProductName = "س", BaseQuantity = -1 });

        var result = BundleValidator.ValidateForRun(bundle, 1);
        Assert.Contains(result.Errors, e => e.Field == nameof(Bundle.Name));
        Assert.Contains(result.Errors, e => e.Field == nameof(BundleItem.BaseQuantity));
    }
}
