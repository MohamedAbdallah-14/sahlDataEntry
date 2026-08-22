using SahelBundleKeyboard.Core.Models;
using SahelBundleKeyboard.Core.Sequencing;

namespace SahelBundleKeyboard.Core.Tests.Sequencing;

public class SequenceBuilderTests
{
    private static BundleItem Item(
        string name,
        decimal qty,
        string? code = null,
        decimal? price = null,
        int order = 0)
    {
        return new BundleItem
        {
            ProductName = name,
            BaseQuantity = qty,
            ProductCode = code ?? string.Empty,
            CustomPrice = price,
            Order = order
        };
    }

    private static IReadOnlyList<InputAction> BuildOneItem(BundleItem item, int count = 1, int delay = 100)
    {
        var bundle = new Bundle { Name = "اختبار" };
        bundle.Items.Add(item);
        return SequenceBuilder.Build(bundle, count, delay);
    }

    [Fact]
    public void SingleItem_ProducesExactActionOrder_WithCode()
    {
        var actions = BuildOneItem(Item("زيت عافية", 2, code: "6290100000001"), delay: 150);

        var expected = new InputAction[]
        {
            new TypeTextAction("6290100000001") { ItemIndex = 0 },
            new PressEnterAction { ItemIndex = 0 },
            new WaitAction(150) { ItemIndex = 0 },
            new TypeTextAction("2") { ItemIndex = 0 },
            new PressEnterAction { ItemIndex = 0 },
            new WaitAction(150) { ItemIndex = 0 },
            new PressEnterAction { ItemIndex = 0 },
            new WaitAction(150) { ItemIndex = 0 },
            new PressEnterAction { ItemIndex = 0 },
            new WaitAction(150) { ItemIndex = 0 },
            new PressEnterAction { ItemIndex = 0 },
            new WaitAction(150) { ItemIndex = 0 },
            new PressEnterAction { ItemIndex = 0 },
            new WaitAction(150) { ItemIndex = 0 },
            new PressEnterAction { ItemIndex = 0 },
            new WaitAction(150) { ItemIndex = 0 }
        };

        Assert.Equal(expected.Length, actions.Count);
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], actions[i]);
        }
    }

    [Fact]
    public void ProductCode_HasPriorityOverName()
    {
        var actions = BuildOneItem(Item("زيت عافية", 1, code: "12345"));
        var first = Assert.IsType<TypeTextAction>(actions[0]);
        Assert.Equal("12345", first.Text);
    }

    [Fact]
    public void MissingOrWhitespaceCode_FallsBackToName()
    {
        var without = BuildOneItem(Item("زيت عافية", 1));
        Assert.Equal("زيت عافية", Assert.IsType<TypeTextAction>(without[0]).Text);

        var whitespace = BuildOneItem(Item("زيت عافية", 1, code: "   "));
        Assert.Equal("زيت عافية", Assert.IsType<TypeTextAction>(whitespace[0]).Text);
    }

    [Fact]
    public void QuantityIsMultipliedByBundleCount_AsDecimal()
    {
        var actions = BuildOneItem(Item("سكر", 1.5m), count: 3);
        // type, enter, wait, type-qty, enter, wait, enter, wait
        var qty = Assert.IsType<TypeTextAction>(actions[3]);
        Assert.Equal("4.5", qty.Text);
    }

    [Fact]
    public void DecimalMultiplication_TrimsTrailingZeros()
    {
        var actions = BuildOneItem(Item("سكر", 2.500m), count: 2);
        Assert.Equal("5", Assert.IsType<TypeTextAction>(actions[3]).Text);
    }

    [Fact]
    public void CustomPrice_IsTypedBeforeFinalEnter()
    {
        var actions = BuildOneItem(Item("شاي", 2, price: 17.50m));

        // ... enter(qty), wait, type-price, enter, wait
        var price = Assert.IsType<TypeTextAction>(actions[6]);
        Assert.Equal("17.5", price.Text);
        Assert.IsType<PressEnterAction>(actions[7]);

        var types = actions.OfType<TypeTextAction>().ToList();
        Assert.Equal(3, types.Count);
        Assert.Equal(new[] { "شاي", "2", "17.5" }, types.Select(t => t.Text));
    }

    [Fact]
    public void NoCustomPrice_TypesNothingForPrice_ButStillPressesEnter()
    {
        var actions = BuildOneItem(Item("أرز", 2));
        var enters = actions.OfType<PressEnterAction>().Count();
        Assert.Equal(7, enters);
        Assert.Equal(2, actions.OfType<TypeTextAction>().Count());
        // The final confirmation sequence is present even with no typed price.
        Assert.IsType<PressEnterAction>(actions[^2]);
        Assert.IsType<WaitAction>(actions[^1]);
    }

    [Fact]
    public void FinalConfirmation_PressesEnterFiveTimes_WithDelayAfterEachPress()
    {
        var actions = BuildOneItem(Item("أرز", 2), delay: 75);
        var finalActions = actions.Skip(6).ToArray();

        Assert.Equal(10, finalActions.Length);
        for (var i = 0; i < finalActions.Length; i += 2)
        {
            Assert.IsType<PressEnterAction>(finalActions[i]);
            var wait = Assert.IsType<WaitAction>(finalActions[i + 1]);
            Assert.Equal(75, wait.DelayMilliseconds);
        }
    }

    [Fact]
    public void MultipleItems_AreTypedInSavedOrder_WithDuplicatesPreserved()
    {
        var bundle = new Bundle { Name = "عرض" };
        bundle.Items.Add(new BundleItem { ProductName = "الأول", BaseQuantity = 1, Order = 0 });
        bundle.Items.Add(new BundleItem { ProductName = "الثاني", BaseQuantity = 2, Order = 1 });
        bundle.Items.Add(new BundleItem { ProductName = "الأول", BaseQuantity = 1, Order = 2 }); // duplicate on purpose

        var actions = SequenceBuilder.Build(bundle, 1, 0);

        // Per item without a price: [search, qty] texts alternate; searches sit at even indices.
        var texts = actions.OfType<TypeTextAction>().Select(t => t.Text).ToList();
        var searches = texts.Where((_, idx) => idx % 2 == 0).ToList();

        Assert.Equal(new[] { "الأول", "الثاني", "الأول" }, searches);
        Assert.All(actions.Take(16), a => Assert.Equal(0, a.ItemIndex));
        Assert.All(actions.Skip(16).Take(16), a => Assert.Equal(1, a.ItemIndex));
        Assert.All(actions.Skip(32), a => Assert.Equal(2, a.ItemIndex));
    }

    [Fact]
    public void ItemsAreOrderedByOrderField_RegardlessOfListOrder()
    {
        var bundle = new Bundle { Name = "ترتيب" };
        bundle.Items.Add(new BundleItem { ProductName = "ثانياً", BaseQuantity = 1, Order = 1 });
        bundle.Items.Add(new BundleItem { ProductName = "أولاً", BaseQuantity = 1, Order = 0 });

        var actions = SequenceBuilder.Build(bundle, 1, 0);
        Assert.Equal("أولاً", Assert.IsType<TypeTextAction>(actions[0]).Text);
        Assert.Equal("ثانياً", Assert.IsType<TypeTextAction>(actions[16]).Text);
    }

    [Fact]
    public void ZeroDelay_BuildsWaitActionsWithZeroMilliseconds()
    {
        var actions = BuildOneItem(Item("x", 1), delay: 0);
        Assert.All(actions.OfType<WaitAction>(), w => Assert.Equal(0, w.DelayMilliseconds));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void InvalidCount_Throws(int count)
    {
        var bundle = new Bundle { Name = "b" };
        bundle.Items.Add(Item("x", 1));
        Assert.ThrowsAny<ArgumentException>(() => SequenceBuilder.Build(bundle, count, 100));
    }

    [Fact]
    public void EmptyBundle_Throws()
    {
        var bundle = new Bundle { Name = "فارغة" };
        Assert.Throws<InvalidOperationException>(() => SequenceBuilder.Build(bundle, 1, 100));
    }
}
