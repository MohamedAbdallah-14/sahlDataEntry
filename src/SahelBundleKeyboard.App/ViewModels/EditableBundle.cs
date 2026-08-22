using System.Collections.ObjectModel;
using SahelBundleKeyboard.App.Infrastructure;
using SahelBundleKeyboard.Core.Models;

namespace SahelBundleKeyboard.App.ViewModels;

/// <summary>Editable wrapper around Bundle for the bundles list.</summary>
public sealed class EditableBundle : ObservableObject
{
    private readonly Bundle _bundle;
    private string _name;
    private string _itemsSummary = string.Empty;

    public EditableBundle(Bundle bundle)
    {
        _bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
        _name = bundle.Name;
        RefreshItemsSummary();
    }

    public Bundle Model => _bundle;

    public Guid Id => _bundle.Id;

    public string Name
    {
        get => _name;
        set
        {
            if (SetProperty(ref _name, value))
            {
                _bundle.Name = value;
                OnPropertyChanged();
            }
        }
    }

    public ObservableCollection<EditableBundleItem> Items { get; } = [];

    /// <summary>"3 أصناف" style summary for the list.</summary>
    public string ItemsSummary
    {
        get => _itemsSummary;
        private set => SetProperty(ref _itemsSummary, value);
    }

    public void RefreshItemsSummary()
    {
        ItemsSummary = $"{Items.Count} {PluralizeItems(Items.Count)}";
    }

    private static string PluralizeItems(int count) => count switch
    {
        0 => "أصناف",
        1 => "صنف",
        2 => "صنفان",
        var n when n <= 10 => "أصناف",
        _ => "صنفاً"
    };

    /// <summary>Synchronizes the wrapper collection with model order values after reordering.</summary>
    public void ReapplyOrder()
    {
        for (var i = 0; i < Items.Count; i++)
        {
            Items[i].Order = i;
            _bundle.Items[i] = Items[i].Model;
            _bundle.Items[i].Order = i;
        }

        RefreshItemsSummary();
    }

    public static EditableBundle FromModel(Bundle bundle)
    {
        var editable = new EditableBundle(bundle);
        foreach (var item in bundle.Items.OrderBy(i => i.Order))
        {
            editable.Items.Add(EditableBundleItem.FromModel(item));
        }

        return editable;
    }
}
