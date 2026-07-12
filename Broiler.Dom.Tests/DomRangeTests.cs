namespace Broiler.Dom.Tests;

public sealed class DomRangeTests
{
    private static DomDocument HtmlDocument(out DomElement body)
    {
        var document = new DomDocument();
        var html = document.CreateElement("html");
        body = document.CreateElement("body");
        document.AppendChild(html);
        html.AppendChild(body);
        return document;
    }

    [Fact]
    public void SetStart_And_SetEnd_Expose_The_Boundary_Points()
    {
        var document = HtmlDocument(out var body);
        var a = document.CreateElement("a");
        var b = document.CreateElement("b");
        body.AppendChild(a);
        body.AppendChild(b);

        using var range = new DomRange(body);
        range.SetStart(body, 0);
        range.SetEnd(body, 2);

        Assert.Same(body, range.StartContainer);
        Assert.Equal(0, range.StartOffset);
        Assert.Same(body, range.EndContainer);
        Assert.Equal(2, range.EndOffset);
        Assert.False(range.Collapsed);
    }

    [Fact]
    public void SetStart_After_End_Collapses_Onto_The_New_Start()
    {
        var document = HtmlDocument(out var body);
        var span = document.CreateElement("span");
        body.AppendChild(span);

        using var range = new DomRange(body);
        // Default end is (body, 0); a start after it collapses the range.
        range.SetStart(body, 1);

        Assert.Same(body, range.StartContainer);
        Assert.Equal(1, range.StartOffset);
        Assert.Same(body, range.EndContainer);
        Assert.Equal(1, range.EndOffset);
        Assert.True(range.Collapsed);
    }

    [Fact]
    public void SetEnd_Before_Start_Collapses_Onto_The_New_End()
    {
        var document = HtmlDocument(out var body);
        body.AppendChild(document.CreateElement("a"));
        body.AppendChild(document.CreateElement("b"));

        using var range = new DomRange(body);
        range.SetStart(body, 2);
        range.SetEnd(body, 1); // before start -> collapses onto (body, 1)

        Assert.Equal(1, range.StartOffset);
        Assert.Equal(1, range.EndOffset);
        Assert.True(range.Collapsed);
    }

    [Fact]
    public void Removing_A_Subtree_Adjusts_Boundaries_Per_The_Range_Removing_Steps()
    {
        var document = HtmlDocument(out var body);
        var section = document.CreateElement("section");
        var text = document.CreateTextNode("value");
        var aside = document.CreateElement("aside");
        body.AppendChild(section);
        section.AppendChild(text);
        body.AppendChild(aside);

        using var range = new DomRange(body);
        range.SetStart(text, 2); // start inside the subtree to be removed
        range.SetEnd(body, 1);   // end after section

        body.RemoveChild(section);

        // Start (inside removed subtree) moves to (parent, index); end (in parent,
        // offset past the removed index) decrements. Both land at (body, 0).
        Assert.Same(body, range.StartContainer);
        Assert.Equal(0, range.StartOffset);
        Assert.Same(body, range.EndContainer);
        Assert.Equal(0, range.EndOffset);
        Assert.True(range.Collapsed);
    }

    [Fact]
    public void Removing_An_Earlier_Sibling_Decrements_A_Following_Offset()
    {
        var document = HtmlDocument(out var body);
        var first = document.CreateElement("i");
        var second = document.CreateElement("j");
        body.AppendChild(first);
        body.AppendChild(second);

        using var range = new DomRange(body);
        range.SetStart(body, 1);
        range.SetEnd(body, 2);

        body.RemoveChild(first); // index 0 removed; offsets past it shift down

        Assert.Equal(0, range.StartOffset);
        Assert.Equal(1, range.EndOffset);
    }

    // ---- Selection helpers ----------------------------------------------------

    [Fact]
    public void SelectNode_Sets_Boundaries_Around_The_Node()
    {
        var document = HtmlDocument(out var body);
        var a = document.CreateElement("a");
        var b = document.CreateElement("b");
        body.AppendChild(a);
        body.AppendChild(b);

        using var range = new DomRange(body);
        range.SelectNode(b);

        Assert.Same(body, range.StartContainer);
        Assert.Equal(1, range.StartOffset);
        Assert.Same(body, range.EndContainer);
        Assert.Equal(2, range.EndOffset);
    }

    [Fact]
    public void SelectNodeContents_Spans_All_Children()
    {
        var document = HtmlDocument(out var body);
        body.AppendChild(document.CreateElement("a"));
        body.AppendChild(document.CreateElement("b"));

        using var range = new DomRange(body);
        range.SelectNodeContents(body);

        Assert.Same(body, range.StartContainer);
        Assert.Equal(0, range.StartOffset);
        Assert.Equal(2, range.EndOffset);
    }

    [Fact]
    public void CommonAncestorContainer_Is_The_Deepest_Shared_Ancestor()
    {
        var document = HtmlDocument(out var body);
        var section = document.CreateElement("section");
        var a = document.CreateElement("a");
        var b = document.CreateElement("b");
        body.AppendChild(section);
        section.AppendChild(a);
        section.AppendChild(b);

        using var range = new DomRange(body);
        range.SetStart(a, 0);
        range.SetEnd(b, 0);

        Assert.Same(section, range.CommonAncestorContainer);
    }

    // ---- deleteContents -------------------------------------------------------

    [Fact]
    public void DeleteContents_Within_One_Text_Node_Removes_The_Substring()
    {
        var document = HtmlDocument(out var body);
        var text = document.CreateTextNode("Hello world");
        body.AppendChild(text);

        using var range = new DomRange(body);
        range.SetStart(text, 5); // after "Hello"
        range.SetEnd(text, 11);  // end of " world"
        range.DeleteContents();

        Assert.Equal("Hello", text.Data);
        Assert.True(range.Collapsed);
        Assert.Same(text, range.StartContainer);
        Assert.Equal(5, range.StartOffset);
    }

    [Fact]
    public void DeleteContents_Removes_Whole_And_Trims_Partial_Text_Across_Nodes()
    {
        var document = HtmlDocument(out var body);
        var t1 = document.CreateTextNode("aaaa");
        var mid = document.CreateElement("span");
        var t3 = document.CreateTextNode("bbbb");
        body.AppendChild(t1);
        body.AppendChild(mid);
        body.AppendChild(t3);

        using var range = new DomRange(body);
        range.SetStart(t1, 2);
        range.SetEnd(t3, 2);
        range.DeleteContents();

        Assert.Equal("aa", t1.Data);
        Assert.Equal("bb", t3.Data);
        Assert.Null(mid.ParentNode); // fully-contained middle element removed
        Assert.Equal(2, body.ChildNodes.Count);
        Assert.True(range.Collapsed);
    }

    // ---- extractContents ------------------------------------------------------

    [Fact]
    public void ExtractContents_Within_One_Text_Node_Returns_The_Substring()
    {
        var document = HtmlDocument(out var body);
        var text = document.CreateTextNode("Hello world");
        body.AppendChild(text);

        using var range = new DomRange(body);
        range.SetStart(text, 0);
        range.SetEnd(text, 5);
        var fragment = range.ExtractContents();

        Assert.Equal(" world", text.Data);
        Assert.Single(fragment.ChildNodes);
        Assert.Equal("Hello", ((DomText)fragment.ChildNodes[0]).Data);
        Assert.True(range.Collapsed);
    }

    [Fact]
    public void ExtractContents_Splits_Partial_Boundaries_And_Moves_Whole_Nodes()
    {
        var document = HtmlDocument(out var body);
        var p1 = document.CreateElement("p");
        var p1Text = document.CreateTextNode("hello");
        p1.AppendChild(p1Text);
        var mid = document.CreateElement("hr");
        var p2 = document.CreateElement("p");
        var p2Text = document.CreateTextNode("world");
        p2.AppendChild(p2Text);
        body.AppendChild(p1);
        body.AppendChild(mid);
        body.AppendChild(p2);

        using var range = new DomRange(body);
        range.SetStart(p1Text, 2); // "he|llo"
        range.SetEnd(p2Text, 3);   // "wor|ld"
        var fragment = range.ExtractContents();

        // Original tree keeps the un-selected remainders.
        Assert.Equal("he", p1Text.Data);
        Assert.Equal("ld", p2Text.Data);
        Assert.Equal(2, body.ChildNodes.Count); // p1, p2 remain; <hr> moved out

        // Fragment holds: <p>llo</p>, <hr>, <p>wor</p>.
        Assert.Equal(3, fragment.ChildNodes.Count);
        var fp1 = Assert.IsType<DomElement>(fragment.ChildNodes[0]);
        Assert.Equal("llo", ((DomText)fp1.ChildNodes[0]).Data);
        // The fully-contained <hr> is moved (not cloned) into the fragment.
        Assert.Same(mid, fragment.ChildNodes[1]);
        Assert.Same(fragment, mid.ParentNode);
        var fp2 = Assert.IsType<DomElement>(fragment.ChildNodes[2]);
        Assert.Equal("wor", ((DomText)fp2.ChildNodes[0]).Data);

        // Extracted clones are distinct from the originals left in the tree.
        Assert.NotSame(p1, fp1);
        Assert.NotSame(p2, fp2);
        Assert.True(range.Collapsed);
    }

    // ---- cloneContents --------------------------------------------------------

    [Fact]
    public void CloneContents_Copies_Without_Mutating_The_Tree()
    {
        var document = HtmlDocument(out var body);
        var p = document.CreateElement("p");
        var text = document.CreateTextNode("abcdef");
        p.AppendChild(text);
        body.AppendChild(p);

        using var range = new DomRange(body);
        range.SetStart(text, 1);
        range.SetEnd(text, 4);
        var fragment = range.CloneContents();

        // Source untouched.
        Assert.Equal("abcdef", text.Data);
        Assert.False(range.Collapsed);
        // Clone holds "bcd".
        Assert.Single(fragment.ChildNodes);
        Assert.Equal("bcd", ((DomText)fragment.ChildNodes[0]).Data);
    }

    // ---- insertNode -----------------------------------------------------------

    [Fact]
    public void InsertNode_At_Element_Offset_Places_Node_And_Extends_Collapsed_Range()
    {
        var document = HtmlDocument(out var body);
        var a = document.CreateElement("a");
        var b = document.CreateElement("b");
        body.AppendChild(a);
        body.AppendChild(b);

        using var range = new DomRange(body);
        range.SetStart(body, 1); // between a and b (collapsed)
        range.SetEnd(body, 1);

        var inserted = document.CreateElement("span");
        range.InsertNode(inserted);

        Assert.Equal(3, body.ChildNodes.Count);
        Assert.Same(inserted, body.ChildNodes[1]); // a, span, b
        // Collapsed range's end extends to include the inserted node.
        Assert.Same(body, range.EndContainer);
        Assert.Equal(2, range.EndOffset);
    }

    [Fact]
    public void InsertNode_Inside_Text_Splits_The_Text_Node()
    {
        var document = HtmlDocument(out var body);
        var text = document.CreateTextNode("abcd");
        body.AppendChild(text);

        using var range = new DomRange(body);
        range.SetStart(text, 2);
        range.SetEnd(text, 2);

        var inserted = document.CreateElement("img");
        range.InsertNode(inserted);

        // "abcd" split into "ab" + inserted + "cd".
        Assert.Equal(3, body.ChildNodes.Count);
        Assert.Equal("ab", ((DomText)body.ChildNodes[0]).Data);
        Assert.Same(inserted, body.ChildNodes[1]);
        Assert.Equal("cd", ((DomText)body.ChildNodes[2]).Data);
    }

    [Fact]
    public void InsertNode_Rejects_A_Comment_Start_Container()
    {
        var document = HtmlDocument(out var body);
        var comment = document.CreateComment("note");
        body.AppendChild(comment);

        using var range = new DomRange(body);
        range.SetStart(comment, 0);
        range.SetEnd(comment, 0);

        Assert.Throws<DomException>(() => range.InsertNode(document.CreateElement("span")));
    }

    // ---- surroundContents -----------------------------------------------------

    [Fact]
    public void SurroundContents_Wraps_The_Selected_Content()
    {
        var document = HtmlDocument(out var body);
        var text = document.CreateTextNode("abcdef");
        body.AppendChild(text);

        using var range = new DomRange(body);
        range.SetStart(text, 1);
        range.SetEnd(text, 4);

        var wrapper = document.CreateElement("em");
        range.SurroundContents(wrapper);

        // "a" + <em>bcd</em> + "ef"
        Assert.Equal(3, body.ChildNodes.Count);
        Assert.Equal("a", ((DomText)body.ChildNodes[0]).Data);
        var em = Assert.IsType<DomElement>(body.ChildNodes[1]);
        Assert.Same(wrapper, em);
        Assert.Equal("bcd", ((DomText)em.ChildNodes[0]).Data);
        Assert.Equal("ef", ((DomText)body.ChildNodes[2]).Data);
    }

    [Fact]
    public void SurroundContents_Throws_When_A_Non_Text_Node_Is_Partially_Selected()
    {
        var document = HtmlDocument(out var body);
        var p = document.CreateElement("p");
        var inner = document.CreateElement("span");
        p.AppendChild(inner);
        var outside = document.CreateElement("b");
        body.AppendChild(p);
        body.AppendChild(outside);

        using var range = new DomRange(body);
        range.SetStart(inner, 0);  // start deep inside <p>
        range.SetEnd(body, 2);     // end after <p> — <p> is partially selected
        Assert.Throws<DomException>(() => range.SurroundContents(document.CreateElement("em")));
    }
}
