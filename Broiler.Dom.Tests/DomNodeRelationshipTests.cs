namespace Broiler.Dom.Tests;

public sealed class DomNodeRelationshipTests
{
    [Fact]
    public void TextContent_Setter_Replaces_Every_Child_With_One_Text_Node_In_One_Record()
    {
        var (document, body) = CreateBody();
        var paragraph = document.CreateElement("p");
        paragraph.Id = "gone";
        paragraph.AppendChild(document.CreateTextNode("old"));
        var comment = document.CreateComment("note");
        body.AppendChild(paragraph);
        body.AppendChild(comment);
        Assert.Same(paragraph, document.GetElementById("gone"));

        var records = new List<DomMutationRecord>();
        document.Mutated += records.Add;
        body.TextContent = "new";

        var text = Assert.IsType<DomText>(Assert.Single(body.ChildNodes));
        Assert.Equal("new", text.Data);
        Assert.Same(body, text.ParentNode);
        Assert.Null(paragraph.ParentNode);
        Assert.Null(comment.ParentNode);
        Assert.Null(document.GetElementById("gone"));

        // The spec's "replace all" queues a single record carrying both node lists.
        var record = Assert.Single(records);
        Assert.Equal(DomMutationType.ChildList, record.Type);
        Assert.Same(body, record.Target);
        Assert.Same(text, Assert.Single(record.AddedNodes!));
        Assert.Equal(new DomNode[] { paragraph, comment }, record.RemovedNodes!);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void TextContent_Setter_Without_Text_Removes_Every_Child_And_Is_Silent_On_An_Empty_Node(string? value)
    {
        var (document, body) = CreateBody();
        body.AppendChild(document.CreateElement("p"));
        body.AppendChild(document.CreateTextNode("x"));

        body.TextContent = value;
        Assert.Empty(body.ChildNodes);

        var records = new List<DomMutationRecord>();
        document.Mutated += records.Add;
        body.TextContent = value;
        Assert.Empty(records);
    }

    [Fact]
    public void TextContent_Setter_Writes_Character_Data_Fills_Fragments_And_Is_Ignored_By_Documents()
    {
        var (document, body) = CreateBody();
        var text = document.CreateTextNode("before");
        body.AppendChild(text);

        text.TextContent = "after";
        Assert.Equal("after", text.Data);
        Assert.Same(text, Assert.Single(body.ChildNodes));

        var fragment = document.CreateDocumentFragment();
        fragment.AppendChild(document.CreateElement("span"));
        fragment.TextContent = "fragment";
        Assert.Equal("fragment", Assert.IsType<DomText>(Assert.Single(fragment.ChildNodes)).Data);

        document.TextContent = "ignored";
        Assert.Same(body.ParentNode, Assert.Single(document.ChildNodes));
        Assert.Equal("after", document.TextContent);
    }

    [Fact]
    public void TextContent_Setter_Moves_A_Live_Range_Out_Of_The_Removed_Children()
    {
        var (document, body) = CreateBody();
        var first = document.CreateElement("p");
        var inner = document.CreateTextNode("inside");
        first.AppendChild(inner);
        body.AppendChild(first);
        body.AppendChild(document.CreateElement("p"));

        using var range = new DomRange(body);
        range.SetStart(inner, 2);
        range.SetEnd(body, 2);

        body.TextContent = "x";

        Assert.Same(body, range.StartContainer);
        Assert.Equal(0, range.StartOffset);
        Assert.Same(body, range.EndContainer);
        Assert.Equal(0, range.EndOffset);
    }

    [Fact]
    public void Element_Traversal_Skips_Text_And_Comment_Nodes()
    {
        var (document, body) = CreateBody();
        var leading = document.CreateTextNode("a");
        var first = document.CreateElement("p");
        var comment = document.CreateComment("c");
        var second = document.CreateElement("p");
        var trailing = document.CreateTextNode("z");
        foreach (var node in new DomNode[] { leading, first, comment, second, trailing })
            body.AppendChild(node);

        Assert.Same(first, body.FirstElementChild);
        Assert.Same(second, body.LastElementChild);
        Assert.Equal(2, body.ChildElementCount);
        Assert.Equal(new[] { first, second }, body.ChildElements);

        Assert.Null(first.PreviousElementSibling);
        Assert.Same(second, first.NextElementSibling);
        Assert.Same(first, second.PreviousElementSibling);
        Assert.Null(second.NextElementSibling);
        Assert.Same(first, leading.NextElementSibling);
        Assert.Same(first, comment.PreviousElementSibling);
        Assert.Same(second, comment.NextElementSibling);
        Assert.Same(second, trailing.PreviousElementSibling);
    }

    [Fact]
    public void Element_Traversal_Is_Empty_For_Text_Only_Trees_And_Disconnected_Nodes()
    {
        var (document, body) = CreateBody();
        var text = document.CreateTextNode("only text");
        body.AppendChild(text);

        Assert.Null(body.FirstElementChild);
        Assert.Null(body.LastElementChild);
        Assert.Equal(0, body.ChildElementCount);
        Assert.Empty(body.ChildElements);
        Assert.Empty(text.ChildElements);
        Assert.Null(text.PreviousElementSibling);
        Assert.Null(text.NextElementSibling);

        var detached = document.CreateElement("div");
        Assert.Null(detached.ParentElement);
        Assert.Null(detached.PreviousElementSibling);
        Assert.Null(detached.NextElementSibling);
    }

    [Fact]
    public void ParentElement_Is_Null_Under_A_Document_Or_Fragment_And_ChildElements_Is_A_Snapshot()
    {
        var (document, body) = CreateBody();
        var html = Assert.IsType<DomElement>(body.ParentNode);
        Assert.Same(html, body.ParentElement);
        Assert.Null(html.ParentElement);

        var fragment = document.CreateDocumentFragment();
        var span = document.CreateElement("span");
        fragment.AppendChild(span);
        Assert.Null(span.ParentElement);
        Assert.Same(span, fragment.FirstElementChild);

        var before = html.ChildElements;
        html.AppendChild(document.CreateElement("footer"));
        Assert.Single(before);
        Assert.Equal(2, html.ChildElements.Count);
    }

    [Fact]
    public void CompareDocumentPosition_Reports_Ancestors_Descendants_And_Tree_Order()
    {
        var (document, body) = CreateBody();
        var html = body.ParentNode!;
        var first = document.CreateElement("p");
        var firstText = document.CreateTextNode("one");
        var second = document.CreateElement("p");
        var secondText = document.CreateTextNode("two");
        first.AppendChild(firstText);
        second.AppendChild(secondText);
        body.AppendChild(first);
        body.AppendChild(second);

        Assert.Equal(DomDocumentPosition.None, body.CompareDocumentPosition(body));
        Assert.Equal(DomDocumentPosition.Contains | DomDocumentPosition.Preceding, firstText.CompareDocumentPosition(html));
        Assert.Equal(DomDocumentPosition.ContainedBy | DomDocumentPosition.Following, document.CompareDocumentPosition(firstText));
        Assert.Equal(DomDocumentPosition.Following, first.CompareDocumentPosition(second));
        Assert.Equal(DomDocumentPosition.Preceding, second.CompareDocumentPosition(first));
        Assert.Equal(DomDocumentPosition.Following, firstText.CompareDocumentPosition(secondText));
        Assert.Equal(DomDocumentPosition.Preceding, secondText.CompareDocumentPosition(firstText));
        Assert.Equal(DomDocumentPosition.Following, firstText.CompareDocumentPosition(second));
    }

    [Fact]
    public void CompareDocumentPosition_Orders_Disconnected_Trees_Consistently()
    {
        var document = new DomDocument();
        var left = document.CreateElement("div");
        var leftChild = document.CreateElement("span");
        left.AppendChild(leftChild);
        var right = document.CreateElement("div");

        const DomDocumentPosition disconnected = DomDocumentPosition.Disconnected | DomDocumentPosition.ImplementationSpecific;
        var forward = leftChild.CompareDocumentPosition(right);
        var backward = right.CompareDocumentPosition(left);

        Assert.Equal(disconnected, forward & disconnected);
        Assert.Equal(disconnected, backward & disconnected);
        var forwardOrder = forward & ~disconnected;
        var backwardOrder = backward & ~disconnected;
        Assert.Contains(forwardOrder, new[] { DomDocumentPosition.Preceding, DomDocumentPosition.Following });
        Assert.NotEqual(forwardOrder, backwardOrder);
        Assert.Equal(forward, leftChild.CompareDocumentPosition(right));
        Assert.Equal(forward, left.CompareDocumentPosition(right));
    }

    private static (DomDocument Document, DomElement Body) CreateBody()
    {
        var document = new DomDocument();
        var html = document.CreateElement("html");
        var body = document.CreateElement("body");
        document.AppendChild(html);
        html.AppendChild(body);
        return (document, body);
    }
}
