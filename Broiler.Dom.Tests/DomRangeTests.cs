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
}
