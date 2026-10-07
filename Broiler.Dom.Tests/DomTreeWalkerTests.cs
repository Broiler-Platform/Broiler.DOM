namespace Broiler.Dom.Tests;

/// <summary>
/// The tree walker against the DOM Standard's algorithms (§6.2), in particular which nodes the filter
/// is asked about. Acid3's tests 1 and 6 are the first two cases.
/// </summary>
public sealed class DomTreeWalkerTests
{
    private sealed class FilterException : Exception;

    private static DomElement CreateTree(out DomElement head, out DomElement title, out DomElement body)
    {
        var document = new DomDocument();
        var html = document.CreateElement("html");
        head = document.CreateElement("head");
        title = document.CreateElement("title");
        body = document.CreateElement("body");
        document.AppendChild(html);
        html.AppendChild(head);
        head.AppendChild(title);
        html.AppendChild(body);
        return html;
    }

    [Fact]
    public void A_Throwing_Filter_Is_Asked_Only_About_The_Nodes_The_Standard_Visits()
    {
        var html = CreateTree(out var head, out var title, out var body);
        var visited = new List<DomNode>();
        var calls = 0;
        DomFilterResult Filter(DomNode node)
        {
            visited.Add(node);
            calls++;
            return calls switch
            {
                1 or 3 or 4 or 5 or 6 or 11 or 12 => throw new FilterException(),
                _ => DomFilterResult.Accept
            };
        }
        var walker = new DomTreeWalker(html, DomWhatToShow.All, Filter);

        Assert.Throws<FilterException>(() => walker.NextNode());
        Assert.Same(head, walker.NextNode());
        Assert.Throws<FilterException>(() => walker.PreviousNode());
        Assert.Throws<FilterException>(() => walker.FirstChild());
        Assert.Throws<FilterException>(() => walker.LastChild());
        Assert.Throws<FilterException>(() => walker.NextSibling());
        Assert.Equal(6, calls);

        // At the root's child with no previous sibling: the walk reaches the root and stops there
        // without asking the filter about it.
        Assert.Null(walker.PreviousSibling());
        Assert.Equal(6, calls);

        Assert.Same(title, walker.LastChild());
        // Title has no next sibling, so its parent is filtered (accepted here) and the walk stops.
        Assert.Null(walker.NextSibling());
        Assert.Equal(8, calls);
        Assert.Same(head, walker.ParentNode());
        Assert.Same(body, walker.NextSibling());
        Assert.Throws<FilterException>(() => walker.PreviousSibling());
        Assert.Throws<FilterException>(() => walker.ParentNode());
        Assert.Same(body, walker.CurrentNode);
        Assert.Equal([head, head, html, title, title, body, title, head, head, body, head, html], visited);
    }

    [Fact]
    public void A_Walk_Continues_From_A_Current_Node_Outside_The_Root()
    {
        var html = CreateTree(out _, out var title, out var body);
        var p = html.OwnerDocument.CreateElement("p");
        body.AppendChild(p);
        var walker = new DomTreeWalker(body);

        Assert.Same(p, walker.LastChild());
        Assert.Same(body, walker.PreviousNode());
        html.RemoveChild(body);
        Assert.Same(p, walker.LastChild());
        Assert.Null(walker.NextNode());

        // p leaves the root's subtree; the walk goes on from where it now is.
        html.AppendChild(p);
        Assert.Same(title, walker.PreviousNode());
        p.AppendChild(body);
        Assert.Same(p, walker.NextNode());
        Assert.Same(body, walker.NextNode());
        Assert.Null(walker.PreviousNode());
    }

    [Fact]
    public void The_Current_Node_Can_Be_Set_Outside_The_Root()
    {
        CreateTree(out var head, out _, out var body);
        var walker = new DomTreeWalker(body) { CurrentNode = head };

        Assert.Same(head, walker.CurrentNode);
        Assert.Same(body, walker.NextSibling());
    }

    [Fact]
    public void Children_And_Siblings_Climb_Out_Of_A_Skipped_Subtree()
    {
        var document = new DomDocument();
        var root = document.CreateElement("root");
        var skipped = document.CreateElement("skip");
        var rejected = document.CreateElement("reject");
        var target = document.CreateElement("target");
        root.AppendChild(skipped);
        skipped.AppendChild(rejected);
        root.AppendChild(target);
        DomFilterResult Filter(DomNode node) => node switch
        {
            DomElement { LocalName: "skip" } => DomFilterResult.Skip,
            DomElement { LocalName: "reject" } => DomFilterResult.Reject,
            _ => DomFilterResult.Accept
        };

        var walker = new DomTreeWalker(root, DomWhatToShow.All, Filter);
        Assert.Same(target, walker.FirstChild());

        var first = document.CreateElement("first");
        root.InsertBefore(first, skipped);
        walker.CurrentNode = first;
        Assert.Same(target, walker.NextSibling());
    }

    [Fact]
    public void Next_Node_Does_Not_Filter_The_Node_It_Starts_From()
    {
        var html = CreateTree(out var head, out _, out _);
        var walker = new DomTreeWalker(html, DomWhatToShow.All,
            node => ReferenceEquals(node, html) ? DomFilterResult.Reject : DomFilterResult.Accept);

        Assert.Same(head, walker.NextNode());
    }
}
