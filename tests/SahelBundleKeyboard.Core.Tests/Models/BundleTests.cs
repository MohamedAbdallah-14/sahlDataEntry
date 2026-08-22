using SahelBundleKeyboard.Core.Models;

namespace SahelBundleKeyboard.Core.Tests.Models;

public class BundleTests
{
    [Fact]
    public void DeepClone_CopiesItemsWithNewIds_PreservingOrderAndValues()
    {
        var bundle = new Bundle { Name = "عرض الأضحية" };
        bundle.Items.Add(new BundleItem
        {
            ProductCode = "62901",
            ProductName = "لحم بلدي",
            BaseQuantity = 2.5m,
            CustomPrice = 300m,
            Order = 0
        });
        bundle.Items.Add(new BundleItem
        {
            ProductName = "لحم بلدي", // duplicate row is intentional and must survive cloning
            BaseQuantity = 1,
            Order = 1
        });

        var clone = bundle.DeepClone();

        Assert.NotEqual(bundle.Id, clone.Id);
        Assert.Equal(bundle.Name, clone.Name);
        Assert.Equal(2, clone.Items.Count);
        Assert.Equal("لحم بلدي", clone.Items[1].ProductName); // duplicates preserved

        for (var i = 0; i < bundle.Items.Count; i++)
        {
            Assert.NotEqual(bundle.Items[i].Id, clone.Items[i].Id);
            Assert.Equal(bundle.Items[i].ProductName, clone.Items[i].ProductName);
            Assert.Equal(bundle.Items[i].ProductCode, clone.Items[i].ProductCode);
            Assert.Equal(bundle.Items[i].BaseQuantity, clone.Items[i].BaseQuantity);
            Assert.Equal(bundle.Items[i].CustomPrice, clone.Items[i].CustomPrice);
            Assert.Equal(bundle.Items[i].Order, clone.Items[i].Order);
        }
    }

    [Fact]
    public void NewEntities_GetStableUniqueIds()
    {
        var a = new BundleItem();
        var b = new BundleItem();
        Assert.NotEqual(a.Id, b.Id);
        Assert.NotEqual(Guid.Empty, a.Id);
    }
}
