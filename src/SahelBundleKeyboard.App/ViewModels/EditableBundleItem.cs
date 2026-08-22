using System.ComponentModel;
using SahelBundleKeyboard.Core.Models;
using SahelBundleKeyboard.Core.Sequencing;

namespace SahelBundleKeyboard.App.ViewModels;

/// <summary>Editable wrapper around BundleItem for the DataGrid, with Arabic cell validation.</summary>
public sealed class EditableBundleItem : INotifyPropertyChanged, IDataErrorInfo
{
    private readonly BundleItem _item;
    private string _baseQuantityText = "1";
    private string _customPriceText = string.Empty;

    public EditableBundleItem(BundleItem item)
    {
        _item = item ?? throw new ArgumentNullException(nameof(item));
        BaseQuantityText = QuantityFormatter.Format(item.BaseQuantity);
        CustomPriceText = item.CustomPrice.HasValue ? QuantityFormatter.Format(item.CustomPrice.Value) : string.Empty;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public BundleItem Model => _item;

    public Guid Id => _item.Id;

    public string ProductCode
    {
        get => _item.ProductCode;
        set
        {
            _item.ProductCode = value;
            OnPropertyChanged(nameof(ProductCode));
        }
    }

    public string ProductName
    {
        get => _item.ProductName;
        set
        {
            _item.ProductName = value;
            OnPropertyChanged(nameof(ProductName));
        }
    }

    /// <summary>Raw text as typed; parsed on commit so a bad value never silently mutates data.</summary>
    public string BaseQuantityText
    {
        get => _baseQuantityText;
        set
        {
            _baseQuantityText = value;
            OnPropertyChanged(nameof(BaseQuantityText));
        }
    }

    /// <summary>Raw text for custom price; empty means "no custom price".</summary>
    public string CustomPriceText
    {
        get => _customPriceText;
        set
        {
            _customPriceText = value;
            OnPropertyChanged(nameof(CustomPriceText));
        }
    }

    public int Order
    {
        get => _item.Order;
        set
        {
            _item.Order = value;
            OnPropertyChanged(nameof(Order));
        }
    }

    public string this[string columnName] => columnName switch
    {
        nameof(ProductName) => ProductName.Trim().Length == 0 ? "اسم المنتج مطلوب" : string.Empty,
        nameof(BaseQuantityText) => ValidateQuantity(),
        nameof(CustomPriceText) => ValidatePrice(),
        _ => string.Empty
    };

    public string Error => string.Empty;

    private string ValidateQuantity()
    {
        if (!QuantityFormatter.TryParse(BaseQuantityText, out var quantity))
        {
            return "أدخل كمية رقمية مثل 2 أو 1.5";
        }

        return quantity <= 0 ? "الكمية يجب أن تكون أكبر من صفر" : string.Empty;
    }

    private string ValidatePrice()
    {
        var text = CustomPriceText.Trim();
        if (text.Length == 0)
        {
            return string.Empty; // optional field
        }

        if (!QuantityFormatter.TryParse(text, out var price))
        {
            return "أدخل سعراً رقمياً أو اتركه فارغاً";
        }

        return price < 0 ? "السعر لا يمكن أن يكون سالباً" : string.Empty;
    }

    public bool HasValidationError =>
        this[nameof(ProductName)].Length > 0 ||
        this[nameof(BaseQuantityText)].Length > 0 ||
        this[nameof(CustomPriceText)].Length > 0;

    /// <summary>
    /// Commits text fields into the underlying model only when every value parses.
    /// Returns false with an Arabic message otherwise; nothing is mutated then.
    /// </summary>
    public bool TryCommit(out string error)
    {
        error = string.Empty;

        if (this[nameof(ProductName)] is { Length: > 0 } nameError)
        {
            error = nameError;
            return false;
        }

        if (!QuantityFormatter.TryParse(BaseQuantityText, out var quantity))
        {
            error = $"الكمية غير صالحة في صف \"{DisplayName}\".";
            return false;
        }

        if (quantity <= 0)
        {
            error = $"الكمية يجب أن تكون أكبر من صفر في صف \"{DisplayName}\".";
            return false;
        }

        decimal? price = null;
        var priceText = CustomPriceText.Trim();
        if (priceText.Length > 0)
        {
            if (!QuantityFormatter.TryParse(priceText, out var parsedPrice))
            {
                error = $"السعر المخصص غير صالح في صف \"{DisplayName}\".";
                return false;
            }

            if (parsedPrice < 0)
            {
                error = $"السعر المخصص لا يمكن أن يكون سالباً في صف \"{DisplayName}\".";
                return false;
            }

            price = parsedPrice;
        }

        _item.BaseQuantity = quantity;
        _item.CustomPrice = price;
        return true;
    }

    public void RefreshFromModel()
    {
        OnPropertyChanged(nameof(ProductCode));
        OnPropertyChanged(nameof(ProductName));
        OnPropertyChanged(nameof(Order));
        BaseQuantityText = QuantityFormatter.Format(_item.BaseQuantity);
        CustomPriceText = _item.CustomPrice.HasValue ? QuantityFormatter.Format(_item.CustomPrice.Value) : string.Empty;
    }

    public static EditableBundleItem FromModel(BundleItem item) => new(item);

    public string DisplayName => ProductName.Trim().Length > 0 ? ProductName.Trim() : "(بدون اسم)";

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
