namespace Broiler.Dom.Tests;

public sealed class DomMutationObserverFilterTests
{
    private static (DomElement Root, DomElement Child, DomElement Grandchild) Tree()
    {
        var document = new DomDocument();
        var root = document.CreateElement("div");
        var child = document.CreateElement("span");
        var grandchild = document.CreateElement("b");
        document.AppendChild(root);
        root.AppendChild(child);
        child.AppendChild(grandchild);
        return (root, child, grandchild);
    }

    [Fact]
    public void Type_Flag_Gates_Delivery()
    {
        var (root, _, _) = Tree();
        var childListRecord = new DomMutationRecord(DomMutationType.ChildList, root);

        Assert.True(DomMutationObserverFilter.Matches(childListRecord, root, new DomMutationObserverOptions { ChildList = true }));
        Assert.False(DomMutationObserverFilter.Matches(childListRecord, root, new DomMutationObserverOptions { Attributes = true }));
    }

    [Fact]
    public void Attributes_And_CharacterData_Types_Are_Gated_By_Their_Flags()
    {
        var (root, _, _) = Tree();
        var attr = new DomMutationRecord(DomMutationType.Attributes, root, AttributeName: "class");
        var cdata = new DomMutationRecord(DomMutationType.CharacterData, root);

        Assert.True(DomMutationObserverFilter.Matches(attr, root, new DomMutationObserverOptions { Attributes = true }));
        Assert.False(DomMutationObserverFilter.Matches(attr, root, new DomMutationObserverOptions { CharacterData = true }));
        Assert.True(DomMutationObserverFilter.Matches(cdata, root, new DomMutationObserverOptions { CharacterData = true }));
    }

    [Fact]
    public void Without_Subtree_Only_The_Observed_Node_Matches()
    {
        var (root, child, _) = Tree();
        var record = new DomMutationRecord(DomMutationType.Attributes, child, AttributeName: "id");
        var options = new DomMutationObserverOptions { Attributes = true };

        Assert.True(DomMutationObserverFilter.Matches(record, child, options));   // self
        Assert.False(DomMutationObserverFilter.Matches(record, root, options));   // ancestor without subtree
    }

    [Fact]
    public void Subtree_Extends_Matching_To_Ancestors_Of_The_Target()
    {
        var (root, child, grandchild) = Tree();
        var record = new DomMutationRecord(DomMutationType.Attributes, grandchild, AttributeName: "id");
        var options = new DomMutationObserverOptions { Attributes = true, Subtree = true };

        Assert.True(DomMutationObserverFilter.Matches(record, root, options));       // ancestor + subtree
        Assert.True(DomMutationObserverFilter.Matches(record, child, options));      // nearer ancestor + subtree
        Assert.True(DomMutationObserverFilter.Matches(record, grandchild, options)); // self
    }

    [Fact]
    public void A_Sibling_Or_Unrelated_Node_Never_Matches()
    {
        var document = new DomDocument();
        var root = document.CreateElement("div");
        var a = document.CreateElement("a");
        var b = document.CreateElement("b");
        document.AppendChild(root);
        root.AppendChild(a);
        root.AppendChild(b);

        var record = new DomMutationRecord(DomMutationType.ChildList, a);
        // Even with subtree, an observer on a sibling never sees a's mutation.
        Assert.False(DomMutationObserverFilter.Matches(record, b, new DomMutationObserverOptions { ChildList = true, Subtree = true }));
    }

    [Theory]
    [InlineData("class", true)]
    [InlineData("id", false)]
    public void AttributeFilter_Restricts_To_Listed_Names(string mutatedAttribute, bool expected)
    {
        var (root, _, _) = Tree();
        var record = new DomMutationRecord(DomMutationType.Attributes, root, AttributeName: mutatedAttribute);
        var options = new DomMutationObserverOptions { Attributes = true, AttributeFilter = ["class", "data-x"] };

        Assert.Equal(expected, DomMutationObserverFilter.Matches(record, root, options));
    }

    [Fact]
    public void CapturesOldValue_Follows_The_OldValue_Flags()
    {
        var (root, _, _) = Tree();
        var attr = new DomMutationRecord(DomMutationType.Attributes, root, AttributeName: "class");
        var cdata = new DomMutationRecord(DomMutationType.CharacterData, root);

        Assert.True(DomMutationObserverFilter.CapturesOldValue(attr, new DomMutationObserverOptions { AttributeOldValue = true }));
        Assert.False(DomMutationObserverFilter.CapturesOldValue(attr, new DomMutationObserverOptions { CharacterDataOldValue = true }));
        Assert.True(DomMutationObserverFilter.CapturesOldValue(cdata, new DomMutationObserverOptions { CharacterDataOldValue = true }));
    }
}
