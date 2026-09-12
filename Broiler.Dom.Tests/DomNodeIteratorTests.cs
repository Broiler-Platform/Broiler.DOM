namespace Broiler.Dom.Tests;

public sealed class DomNodeIteratorTests
{
    [Fact]
    public void Unchanged_Tree_Can_Be_Traversed_In_Both_Directions()
    {
        var document = new DomDocument();
        var root = document.CreateElement("div");
        for (var index = 0; index < 100; index++)
            root.AppendChild(document.CreateElement("span"));
        var expected = root.InclusiveDescendants().ToArray();
        using var iterator = new DomNodeIterator(root);

        foreach (var node in expected)
            Assert.Same(node, iterator.NextNode());
        Assert.Null(iterator.NextNode());
        foreach (var node in expected.Reverse())
            Assert.Same(node, iterator.PreviousNode());
        Assert.Null(iterator.PreviousNode());
    }

    [Fact]
    public void Filter_Can_Remove_A_Rejected_Candidate_And_Insert_Its_Successor()
    {
        var document = new DomDocument();
        var root = document.CreateElement("div");
        var removed = document.CreateElement("remove");
        var tail = document.CreateElement("tail");
        var inserted = document.CreateElement("inserted");
        root.AppendChild(removed);
        root.AppendChild(tail);
        using var iterator = new DomNodeIterator(root, filter: node =>
        {
            if (ReferenceEquals(node, removed))
            {
                removed.Remove();
                root.InsertBefore(inserted, tail);
                return DomFilterResult.Reject;
            }
            return ReferenceEquals(node, root) ? DomFilterResult.Skip : DomFilterResult.Accept;
        });

        Assert.Same(inserted, iterator.NextNode());
        Assert.Same(tail, iterator.NextNode());
        Assert.Null(iterator.NextNode());
        Assert.Same(tail, iterator.PreviousNode());
        Assert.Same(inserted, iterator.PreviousNode());
        Assert.Null(iterator.PreviousNode());
    }

    [Fact]
    public void Backward_Filter_Can_Remove_Several_Nodes_Without_Using_Stale_Indices()
    {
        var document = new DomDocument();
        var root = document.CreateElement("div");
        var first = document.CreateElement("first");
        var second = document.CreateElement("second");
        var last = document.CreateElement("last");
        root.AppendChild(first);
        root.AppendChild(second);
        root.AppendChild(last);
        var remove = false;
        using var iterator = new DomNodeIterator(root, filter: node =>
        {
            if (remove && ReferenceEquals(node, last))
            {
                second.Remove();
                last.Remove();
                return DomFilterResult.Reject;
            }
            return DomFilterResult.Accept;
        });
        Assert.Same(root, iterator.NextNode());
        Assert.Same(first, iterator.NextNode());
        Assert.Same(second, iterator.NextNode());
        Assert.Same(last, iterator.NextNode());
        remove = true;

        Assert.Same(first, iterator.PreviousNode());
        Assert.Same(root, iterator.PreviousNode());
        Assert.Null(iterator.PreviousNode());
    }

    [Fact]
    public void Moving_Children_In_A_Detached_Root_Refreshes_Traversal_Order()
    {
        var document = new DomDocument();
        var root = document.CreateElement("div");
        var first = document.CreateElement("first");
        var second = document.CreateElement("second");
        root.AppendChild(first);
        root.AppendChild(second);
        using var iterator = new DomNodeIterator(root);
        Assert.Same(root, iterator.NextNode());

        root.MoveBefore(second, first);

        Assert.Same(second, iterator.NextNode());
        Assert.Same(first, iterator.NextNode());
        Assert.Null(iterator.NextNode());
    }

    [Fact]
    public void Duplicate_Ids_Still_Resolve_In_Tree_Order_After_Moves_And_Removal()
    {
        var document = new DomDocument();
        var root = document.CreateElement("div");
        document.AppendChild(root);
        var first = document.CreateElement("first");
        var second = document.CreateElement("second");
        first.Id = second.Id = "match";
        root.AppendChild(first);
        Assert.Same(first, document.GetElementById("match"));
        root.AppendChild(second);
        Assert.Same(first, document.GetElementById("match"));
        root.MoveBefore(second, first);
        Assert.Same(second, document.GetElementById("match"));
        second.Remove();
        Assert.Same(first, document.GetElementById("match"));
        first.Id = "renamed";
        Assert.Null(document.GetElementById("match"));
        Assert.Same(first, document.GetElementById("renamed"));
    }
}
