namespace Broiler.Dom.Tests;

/// <summary>
/// <see cref="DomDocument.Removing"/>: what a host has to do before nodes leave the document -- a
/// browser blurs the focused element there -- and what a removal does when that host's script moved
/// the node first. Chromium's behaviour, measured: <c>blur</c> and <c>focusout</c> fire while the
/// element is still connected and its parent unchanged; a <c>blur</c> handler that moves it makes the
/// removal throw <c>NotFoundError</c> "The node to be removed is no longer a child of this node.
/// Perhaps it was moved in a 'blur' event handler?".
/// </summary>
public sealed class DomRemovingTests
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

    [Fact(Timeout = 600000)]
    public void A_Removal_Is_Announced_While_The_Node_Is_Still_Where_It_Was()
    {
        var document = CreateHtmlDocument(out var body);
        var div = document.CreateElement("div");
        var input = document.CreateElement("input");
        body.AppendChild(div);
        div.AppendChild(input);

        var seen = new List<string>();
        document.Removing += removal => seen.Add(
            $"{((DomElement)removal.Root).LocalName} children={removal.ChildrenOnly} " +
            $"connected={input.IsConnected} parent={(input.ParentNode as DomElement)?.LocalName} " +
            $"removes-input={removal.Removes(input)} removes-body={removal.Removes(body)}");

        body.RemoveChild(div);

        Assert.Equal(["div children=False connected=True parent=div removes-input=True removes-body=False"], seen);
        Assert.Null(div.ParentNode);
        Assert.False(input.IsConnected);
    }

    [Fact(Timeout = 600000)]
    public void Replacing_All_Children_Is_Announced_Once_For_The_Children()
    {
        var document = CreateHtmlDocument(out var body);
        body.AppendChild(document.CreateElement("a"));
        body.AppendChild(document.CreateElement("b"));

        var seen = new List<DomRemoval>();
        document.Removing += seen.Add;

        body.TextContent = "text";
        body.ReplaceChildren(document.CreateElement("c"));

        Assert.Equal(2, seen.Count);
        Assert.All(seen, removal =>
        {
            Assert.Same(body, removal.Root);
            Assert.True(removal.ChildrenOnly);
            Assert.False(removal.Removes(body));
        });
        Assert.Equal("c", ((DomElement)body.FirstChild!).LocalName);
    }

    [Fact(Timeout = 600000)]
    public void Replacing_All_Children_Takes_What_The_Handler_Left()
    {
        var document = CreateHtmlDocument(out var body);
        var a = document.CreateElement("a");
        body.AppendChild(a);
        var late = document.CreateElement("late");

        document.Removing += _ =>
        {
            if (late.ParentNode is null)
                body.AppendChild(late);
        };

        body.TextContent = null;

        Assert.Empty(body.ChildNodes);
        Assert.Null(a.ParentNode);
        Assert.Null(late.ParentNode);
    }

    [Fact(Timeout = 600000)]
    public void A_Node_Moved_By_A_Handler_Is_Not_Removed_From_Where_It_Went()
    {
        var document = CreateHtmlDocument(out var body);
        var from = document.CreateElement("from");
        var to = document.CreateElement("to");
        var input = document.CreateElement("input");
        body.AppendChild(from);
        body.AppendChild(to);
        from.AppendChild(input);

        // As a host does it: focus is taken away before the blur handler runs, so the move the handler
        // makes -- itself a removal -- has no focused element left to blur.
        DomElement? focused = input;
        document.Removing += removal =>
        {
            if (focused is null || !removal.Removes(focused))
                return;

            focused = null;
            to.AppendChild(input);
        };

        var error = Assert.Throws<DomException>(() => from.RemoveChild(input));

        Assert.Equal("NotFoundError", error.Name);
        Assert.Equal(
            "The node to be removed is no longer a child of this node. Perhaps it was moved in a 'blur' event handler?",
            error.Message);
        Assert.Same(to, input.ParentNode);
        Assert.True(input.IsConnected);
    }

    [Fact(Timeout = 600000)]
    public void Moving_A_Node_By_Insertion_Announces_Its_Removal()
    {
        var document = CreateHtmlDocument(out var body);
        var from = document.CreateElement("from");
        var to = document.CreateElement("to");
        var input = document.CreateElement("input");
        body.AppendChild(from);
        body.AppendChild(to);
        from.AppendChild(input);

        var seen = new List<DomNode>();
        document.Removing += removal => seen.Add(removal.Root);

        to.AppendChild(input);

        Assert.Equal([input], seen);
        Assert.Same(to, input.ParentNode);
    }

    [Fact(Timeout = 600000)]
    public void An_Insertion_Whose_Reference_A_Handler_Took_Away_Throws()
    {
        var document = CreateHtmlDocument(out var body);
        var from = document.CreateElement("from");
        var to = document.CreateElement("to");
        var reference = document.CreateElement("ref");
        var input = document.CreateElement("input");
        body.AppendChild(from);
        body.AppendChild(to);
        to.AppendChild(reference);
        from.AppendChild(input);

        document.Removing += removal =>
        {
            if (removal.Removes(input) && reference.ParentNode is not null)
                reference.Remove();
        };

        var error = Assert.Throws<DomException>(() => to.InsertBefore(input, reference));

        Assert.Equal("NotFoundError", error.Name);
        Assert.Null(input.ParentNode);
        Assert.Empty(to.ChildNodes);
    }

    [Fact(Timeout = 600000)]
    public void Detached_Trees_And_Moves_Announce_Nothing()
    {
        var document = CreateHtmlDocument(out var body);
        var detached = document.CreateElement("div");
        var child = document.CreateElement("span");
        detached.AppendChild(child);
        var a = document.CreateElement("a");
        var b = document.CreateElement("b");
        body.AppendChild(a);
        body.AppendChild(b);

        var seen = new List<DomRemoval>();
        document.Removing += seen.Add;

        detached.RemoveChild(child);
        detached.TextContent = "x";
        body.MoveBefore(b, a);

        Assert.Empty(seen);
    }

    [Fact(Timeout = 600000)]
    public void A_Host_Takes_Its_Shadow_Tree_With_It()
    {
        var document = CreateHtmlDocument(out var body);
        var host = document.CreateElement("div");
        body.AppendChild(host);
        var shadow = host.AttachShadow(DomShadowRootMode.Open);
        var inner = document.CreateElement("input");
        shadow.AppendChild(inner);
        var outside = document.CreateElement("p");
        body.AppendChild(outside);

        var removal = new DomRemoval(host, ChildrenOnly: false);
        var childrenOfBody = new DomRemoval(body, ChildrenOnly: true);

        Assert.True(removal.Removes(inner));
        Assert.True(removal.Removes(host));
        Assert.False(removal.Removes(outside));
        Assert.True(childrenOfBody.Removes(inner));
        Assert.False(childrenOfBody.Removes(body));
    }
}
