using SahelBundleKeyboard.Core.Models;

namespace SahelBundleKeyboard.Core.Validation;

/// <summary>
/// Validates bundles and bundle items. Never mutates the data it validates.
/// </summary>
public static class BundleValidator
{
    public static ValidationResult ValidateForRun(Bundle? bundle, int bundleCount)
    {
        var result = ValidateBundle(bundle);

        if (bundleCount < SettingLimits.MinBundleCount)
        {
            result.AddError(nameof(bundleCount), "عدد الحزم يجب أن يكون عدداً صحيحاً موجباً (1 على الأقل).");
        }

        return result;
    }

    public static ValidationResult ValidateBundle(Bundle? bundle)
    {
        var result = new ValidationResult();

        if (bundle is null)
        {
            result.AddError("bundle", "لم يتم اختيار حزمة.");
            return result;
        }

        if (string.IsNullOrWhiteSpace(bundle.Name))
        {
            result.AddError(nameof(Bundle.Name), "اسم الحزمة مطلوب.");
        }
        else if (bundle.Name.Trim().Length > BundleLimits.MaxBundleNameLength)
        {
            result.AddError(nameof(Bundle.Name), $"اسم الحزمة طويل جداً (الحد الأقصى {BundleLimits.MaxBundleNameLength} حرفاً).");
        }

        if (bundle.Items.Count == 0)
        {
            result.AddError(nameof(Bundle.Items), "الحزمة فارغة. أضف صنفاً واحداً على الأقل قبل التشغيل.");
            return result;
        }

        if (bundle.Items.Count > BundleLimits.MaxItemsPerBundle)
        {
            result.AddError(nameof(Bundle.Items), $"عدد الأصناف في الحزمة يتجاوز الحد الأقصى ({BundleLimits.MaxItemsPerBundle}).");
        }

        foreach (var item in bundle.Items)
        {
            ValidateItem(item, result);
        }

        return result;
    }

    /// <summary>Validates a single item row. Used by the grid, the importer, and the backup validator.</summary>
    public static void ValidateItem(BundleItem item, ValidationResult result, string fieldPrefix = "")
    {
        if (item.ProductName.Trim().Length == 0)
        {
            result.AddError(fieldPrefix + nameof(item.ProductName), "اسم المنتج مطلوب في كل صف.");
        }
        else if (item.ProductName.Trim().Length > BundleLimits.MaxProductNameLength)
        {
            result.AddError(fieldPrefix + nameof(item.ProductName), $"اسم المنتج طويل جداً (الحد الأقصى {BundleLimits.MaxProductNameLength} حرفاً).");
        }

        if (item.ProductCode.Trim().Length > BundleLimits.MaxProductCodeLength)
        {
            result.AddError(fieldPrefix + nameof(item.ProductCode), $"كود المنتج طويل جداً (الحد الأقصى {BundleLimits.MaxProductCodeLength} حرفاً).");
        }

        if (item.BaseQuantity <= 0m)
        {
            result.AddError(fieldPrefix + nameof(item.BaseQuantity), "الكمية الأساسية يجب أن تكون أكبر من صفر.");
        }

        if (item.CustomPrice.HasValue && item.CustomPrice.Value < 0m)
        {
            result.AddError(fieldPrefix + nameof(item.CustomPrice), "السعر المخصص لا يمكن أن يكون سالباً.");
        }

        if (item.BaseQuantity > BundleLimits.MaxDecimalValue || (item.CustomPrice ?? 0m) > BundleLimits.MaxDecimalValue)
        {
            result.AddError(fieldPrefix + "Values", "قيمة كمية أو سعر خارج الحد المسموح.");
        }
    }
}
