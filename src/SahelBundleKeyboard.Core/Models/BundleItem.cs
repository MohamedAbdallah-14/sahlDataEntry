namespace SahelBundleKeyboard.Core.Models;

public sealed class BundleItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Optional product code/barcode. When present it takes priority over the name.</summary>
    public string ProductCode { get; set; } = string.Empty;

    /// <summary>Required product name, even when a code exists (used by the operator to identify the row).</summary>
    public string ProductName { get; set; } = string.Empty;

    /// <summary>Base quantity per single bundle. Must be greater than zero.</summary>
    public decimal BaseQuantity { get; set; } = 1m;

    /// <summary>Optional custom unit price. Null means Sahel keeps its own default price.</summary>
    public decimal? CustomPrice { get; set; }

    /// <summary>Explicit order inside the bundle.</summary>
    public int Order { get; set; }

    public BundleItem DeepClone()
    {
        return new BundleItem
        {
            Id = Guid.NewGuid(),
            ProductCode = ProductCode,
            ProductName = ProductName,
            BaseQuantity = BaseQuantity,
            CustomPrice = CustomPrice,
            Order = Order
        };
    }
}
