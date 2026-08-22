using SahelBundleKeyboard.Core.Models;

namespace SahelBundleKeyboard.Core.Sequencing;

/// <summary>
/// Builds the deterministic list of logical input actions for a bundle run.
///
/// Per item, in saved order:
///   1. type product code if present, otherwise the product name
///   2. Enter (Sahel moves to quantity)
///   3. wait delay
///   4. type final quantity = base quantity x bundle count
///   5. Enter (Sahel moves to price)
///   6. wait delay
///   7. type custom price only when present (otherwise nothing; Sahel keeps default price)
///   8. Enter five times, waiting after each press (confirms the item and dismisses
///      any intermittent popup before the next product)
/// </summary>
public static class SequenceBuilder
{
    public static IReadOnlyList<InputAction> Build(Bundle bundle, int bundleCount, int delayMilliseconds)
    {
        if (bundle is null)
        {
            throw new ArgumentNullException(nameof(bundle));
        }

        if (bundleCount < SettingLimits.MinBundleCount)
        {
            throw new ArgumentOutOfRangeException(nameof(bundleCount), "عدد الحزم يجب أن يكون عدداً صحيحاً موجباً.");
        }

        var items = bundle.Items.OrderBy(i => i.Order).ToList();
        if (items.Count == 0)
        {
            throw new InvalidOperationException("الحزمة فارغة ولا يمكن تشغيلها.");
        }

        var actions = new List<InputAction>(items.Count * 8);

        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];

            // 1. code has priority over name; whitespace-only code falls back to name.
            var search = item.ProductCode.Trim().Length > 0 ? item.ProductCode : item.ProductName;
            actions.Add(new TypeTextAction(search) { ItemIndex = index });

            // 2. Enter -> Sahel moves to quantity field.
            actions.Add(new PressEnterAction { ItemIndex = index });

            // 3. global delay.
            actions.Add(new WaitAction(delayMilliseconds) { ItemIndex = index });

            // 4. final quantity, invariant dot format, no clearing keystrokes.
            var finalQuantity = item.BaseQuantity * bundleCount;
            actions.Add(new TypeTextAction(QuantityFormatter.Format(finalQuantity)) { ItemIndex = index });

            // 5. Enter -> Sahel moves to price field.
            actions.Add(new PressEnterAction { ItemIndex = index });

            // 6. global delay.
            actions.Add(new WaitAction(delayMilliseconds) { ItemIndex = index });

            // 7/8. custom price only when explicitly set; blank keeps Sahel's default price.
            if (item.CustomPrice.HasValue)
            {
                actions.Add(new TypeTextAction(QuantityFormatter.Format(item.CustomPrice.Value)) { ItemIndex = index });
            }

            // Confirm the item and dismiss any intermittent Sahel popup. Waiting after
            // every press gives a popup time to appear before the next Enter is sent.
            for (var confirmation = 0; confirmation < 5; confirmation++)
            {
                actions.Add(new PressEnterAction { ItemIndex = index });
                actions.Add(new WaitAction(delayMilliseconds) { ItemIndex = index });
            }
        }

        return actions;
    }

    public static int CountItems(IReadOnlyList<InputAction> actions)
    {
        return actions.Count == 0 ? 0 : actions.Max(a => a.ItemIndex) + 1;
    }
}
