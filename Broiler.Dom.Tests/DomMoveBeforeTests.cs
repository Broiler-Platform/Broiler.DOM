namespace Broiler.Dom.Tests;

/// <summary>
/// The DOM <c>Node.moveBefore()</c> operation — an atomic reposition that, unlike
/// <c>insertBefore</c>, never disconnects the node.
/// <para>
/// REGRESSION GUARD (WPT issue #1491, problem 27):
/// <c>dom/nodes/moveBefore/preserve-render-blocking-style.html</c> moves a render-blocking
/// <c>&lt;style&gt;</c> and asserts the styles survive. With no <c>moveBefore</c> the script threw,
/// the document was never styled, and the test rendered white against Chromium's 100% green.
/// </para>
/// </summary>
public sealed class DomMoveBeforeTests
{
    private static DomDocument CreateHtmlDocument(out DomElement body)
    {
        var document = new DomDocument();
        var html = document.CreateElement("html");
        body = document.CreateElement("body");
        document.AppendChild(html);
        html.AppendChild(body);
        return document;
    }

    // ── Moving within one parent ──────────────────────────────────────────

    [Fact]
    public void Moves_A_Child_Within_Its_Parent()
    {
        var document = CreateHtmlDocument(out var body);
        var a = document.CreateElement("a-el");
        var b = document.CreateElement("b-el");
        var c = document.CreateElement("c-el");
        body.AppendChild(a);
        body.AppendChild(b);
        body.AppendChild(c);

        body.MoveBefore(c, a);

        Assert.Equal([c, a, b], body.ChildNodes);
        Assert.Same(body, c.ParentNode);
    }

    [Fact]
    public void Moving_Before_Null_Appends_Within_The_Same_Parent()
    {
        var document = CreateHtmlDocument(out var body);
        var a = document.CreateElement("a-el");
        var b = document.CreateElement("b-el");
        body.AppendChild(a);
        body.AppendChild(b);

        body.MoveBefore(a, null);

        Assert.Equal([b, a], body.ChildNodes);
    }

    // Per spec the reference collapses to the node's next sibling, making this a no-op
    // rather than an error.
    [Fact]
    public void Moving_A_Node_Before_Itself_Is_A_No_Op()
    {
        var document = CreateHtmlDocument(out var body);
        var a = document.CreateElement("a-el");
        var b = document.CreateElement("b-el");
        body.AppendChild(a);
        body.AppendChild(b);

        body.MoveBefore(a, a);

        Assert.Equal([a, b], body.ChildNodes);
    }

    // ── Moving across parents ─────────────────────────────────────────────

    [Fact]
    public void Moves_A_Child_Across_Parents()
    {
        var document = CreateHtmlDocument(out var body);
        var source = document.CreateElement("source-el");
        var target = document.CreateElement("target-el");
        var moved = document.CreateElement("moved-el");
        var anchor = document.CreateElement("anchor-el");
        body.AppendChild(source);
        body.AppendChild(target);
        source.AppendChild(moved);
        target.AppendChild(anchor);

        target.MoveBefore(moved, anchor);

        Assert.Empty(source.ChildNodes);
        Assert.Equal([moved, anchor], target.ChildNodes);
        Assert.Same(target, moved.ParentNode);
    }

    // The defining property: the node is never disconnected, so anything keyed off
    // connectedness (render-blocking status, iframe content, focus) survives.
    [Fact]
    public void A_Moved_Node_Stays_Connected_Throughout()
    {
        var document = CreateHtmlDocument(out var body);
        var source = document.CreateElement("source-el");
        var target = document.CreateElement("target-el");
        var moved = document.CreateElement("moved-el");
        body.AppendChild(source);
        body.AppendChild(target);
        source.AppendChild(moved);
        Assert.True(moved.IsConnected);

        target.MoveBefore(moved, null);

        Assert.True(moved.IsConnected);
    }

    [Fact]
    public void A_Moved_Subtree_Keeps_Its_Descendants_And_Id_Index()
    {
        var document = CreateHtmlDocument(out var body);
        var source = document.CreateElement("source-el");
        var target = document.CreateElement("target-el");
        var moved = document.CreateElement("moved-el");
        var child = document.CreateElement("child-el");
        child.Id = "kept";
        body.AppendChild(source);
        body.AppendChild(target);
        source.AppendChild(moved);
        moved.AppendChild(child);

        target.MoveBefore(moved, null);

        Assert.Same(child, Assert.Single(moved.ChildNodes));
        Assert.True(child.IsConnected);
        // The id index is not torn down and rebuilt, because connectedness never changed.
        Assert.Same(child, document.GetElementById("kept"));
    }

    // The render-blocking case from the WPT test: a <style> moved across parents keeps
    // its content and stays in the document, so its rules still apply.
    [Fact]
    public void A_Moved_Style_Element_Keeps_Its_Content_And_Connection()
    {
        var document = CreateHtmlDocument(out var body);
        var source = document.CreateElement("div");
        var target = document.CreateElement("div");
        var style = document.CreateElement("style");
        style.AppendChild(document.CreateTextNode("body { background-color: green; }"));
        body.AppendChild(source);
        body.AppendChild(target);
        source.AppendChild(style);

        target.MoveBefore(style, null);

        Assert.Same(target, style.ParentNode);
        Assert.True(style.IsConnected);
        Assert.Equal(
            "body { background-color: green; }",
            Assert.IsType<DomText>(Assert.Single(style.ChildNodes)).Data);
    }

    // ── Observability ─────────────────────────────────────────────────────

    // The spec queues records for both parents: observers still see the move. Only the
    // disconnection is skipped, not the notification.
    [Fact]
    public void Reports_A_Removal_And_An_Insertion_To_Observers()
    {
        var document = CreateHtmlDocument(out var body);
        var source = document.CreateElement("source-el");
        var target = document.CreateElement("target-el");
        var moved = document.CreateElement("moved-el");
        body.AppendChild(source);
        body.AppendChild(target);
        source.AppendChild(moved);

        var records = new List<DomMutationRecord>();
        document.Mutated += records.Add;

        target.MoveBefore(moved, null);

        Assert.Equal(2, records.Count);
        Assert.All(records, record => Assert.Equal(DomMutationType.ChildList, record.Type));
        Assert.Same(source, records[0].Target);
        Assert.Same(moved, Assert.Single(records[0].RemovedNodes));
        Assert.Same(target, records[1].Target);
        Assert.Same(moved, Assert.Single(records[1].AddedNodes));
    }

    // ── Pre-move validity ─────────────────────────────────────────────────

    // A move has nothing to preserve for a node that was never in the tree; the spec
    // rejects it rather than silently behaving like insertBefore.
    [Fact]
    public void Rejects_A_Node_That_Is_Not_Already_In_The_Tree()
    {
        var document = CreateHtmlDocument(out var body);
        var orphan = document.CreateElement("orphan-el");

        Assert.Throws<DomException>(() => body.MoveBefore(orphan, null));
    }

    // Moving across roots would change connectedness, which a move never does.
    [Fact]
    public void Rejects_A_Node_From_A_Different_Root()
    {
        var document = CreateHtmlDocument(out var body);
        var detachedParent = document.CreateElement("detached-el");
        var node = document.CreateElement("node-el");
        detachedParent.AppendChild(node);

        Assert.Throws<DomException>(() => body.MoveBefore(node, null));
    }

    [Fact]
    public void Rejects_A_Reference_That_Is_Not_A_Child()
    {
        var document = CreateHtmlDocument(out var body);
        var target = document.CreateElement("target-el");
        var moved = document.CreateElement("moved-el");
        var stranger = document.CreateElement("stranger-el");
        body.AppendChild(target);
        body.AppendChild(moved);
        body.AppendChild(stranger);

        Assert.Throws<DomException>(() => target.MoveBefore(moved, stranger));
    }

    [Fact]
    public void Rejects_Moving_A_Node_Into_Its_Own_Descendant()
    {
        var document = CreateHtmlDocument(out var body);
        var outer = document.CreateElement("outer-el");
        var inner = document.CreateElement("inner-el");
        body.AppendChild(outer);
        outer.AppendChild(inner);

        Assert.Throws<DomException>(() => inner.MoveBefore(outer, null));
    }

    [Fact]
    public void Rejects_Unmovable_Node_Types()
    {
        var document = CreateHtmlDocument(out var body);
        var fragment = document.CreateDocumentFragment();
        fragment.AppendChild(document.CreateElement("in-fragment"));

        // A fragment has no single identity to preserve across a move.
        Assert.Throws<DomException>(() => body.MoveBefore(fragment, null));
    }

    [Fact]
    public void Moves_Character_Data_Nodes()
    {
        var document = CreateHtmlDocument(out var body);
        var source = document.CreateElement("source-el");
        var target = document.CreateElement("target-el");
        var text = document.CreateTextNode("payload");
        body.AppendChild(source);
        body.AppendChild(target);
        source.AppendChild(text);

        target.MoveBefore(text, null);

        Assert.Same(target, text.ParentNode);
        Assert.Equal("payload", Assert.IsType<DomText>(Assert.Single(target.ChildNodes)).Data);
    }
}
