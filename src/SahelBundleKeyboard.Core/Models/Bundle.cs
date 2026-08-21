namespace SahelBundleKeyboard.Core.Models;

public sealed class Bundle
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    /// <summary>Ordered item rows. Duplicates are intentional and preserved.</summary>
    public List<BundleItem> Items { get; set; } = new();

    public Bundle DeepClone()
    {
        var copy = new Bundle { Id = Guid.NewGuid(), Name = Name };
        copy.Items.AddRange(Items.Select(i => i.DeepClone()));
        return copy;
    }
}
