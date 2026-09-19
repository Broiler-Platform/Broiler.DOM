using System;
using System.Collections.Generic;

namespace Broiler.Dom.Tests;

public sealed class DomTreeMutationTests
{
    [Fact(Timeout = 600000)]
    public void Before_On_Standalone_Node_Is_NoOp()
    {
        var doc = new DomDocument();
        var node = doc.CreateElement("div");
        var other = doc.CreateElement("span");

        node.Before(other);
        Assert.Null(node.ParentNode);
        Assert.Null(other.ParentNode);
    }

    [Fact(Timeout = 600000)]
    public void Before_Inserts_Nodes_Before_Target()
    {
        var doc = new DomDocument();
        var parent = doc.CreateElement("div");
        var a = doc.CreateElement("a");
        var b = doc.CreateElement("b");
        var c = doc.CreateElement("c");
        parent.AppendChild(a);
        parent.AppendChild(c);

        c.Before(b);
        Assert.Equal([a, b, c], parent.ChildNodes);

        var x = doc.CreateElement("x");
        var y = doc.CreateElement("y");
        a.Before(x, y);
        Assert.Equal([x, y, a, b, c], parent.ChildNodes);
    }

    [Fact(Timeout = 600000)]
    public void Before_Handles_Preceding_Sibling_Argument_ViableSibling()
    {
        var doc = new DomDocument();
        var parent = doc.CreateElement("div");
        var a = doc.CreateElement("a");
        var b = doc.CreateElement("b");
        var c = doc.CreateElement("c");
        parent.AppendChild(a);
        parent.AppendChild(b);
        parent.AppendChild(c);

        // Moving 'a' before 'b'
        b.Before(a);
        Assert.Equal([a, b, c], parent.ChildNodes);
    }

    [Fact(Timeout = 600000)]
    public void After_On_Standalone_Node_Is_NoOp()
    {
        var doc = new DomDocument();
        var node = doc.CreateElement("div");
        var other = doc.CreateElement("span");

        node.After(other);
        Assert.Null(node.ParentNode);
        Assert.Null(other.ParentNode);
    }

    [Fact(Timeout = 600000)]
    public void After_Inserts_Nodes_After_Target_And_Handles_LastChild()
    {
        var doc = new DomDocument();
        var parent = doc.CreateElement("div");
        var a = doc.CreateElement("a");
        var c = doc.CreateElement("c");
        parent.AppendChild(a);
        parent.AppendChild(c);

        var b = doc.CreateElement("b");
        a.After(b);
        Assert.Equal([a, b, c], parent.ChildNodes);

        var d = doc.CreateElement("d");
        var e = doc.CreateElement("e");
        c.After(d, e);
        Assert.Equal([a, b, c, d, e], parent.ChildNodes);
    }

    [Fact(Timeout = 600000)]
    public void ReplaceWith_Replaces_Node_With_Multiple_Nodes()
    {
        var doc = new DomDocument();
        var parent = doc.CreateElement("div");
        var a = doc.CreateElement("a");
        var target = doc.CreateElement("target");
        var c = doc.CreateElement("c");
        parent.AppendChild(a);
        parent.AppendChild(target);
        parent.AppendChild(c);

        var x = doc.CreateElement("x");
        var y = doc.CreateElement("y");
        target.ReplaceWith(x, y);

        Assert.Equal([a, x, y, c], parent.ChildNodes);
        Assert.Null(target.ParentNode);
    }

    [Fact(Timeout = 600000)]
    public void ReplaceWith_With_Empty_Arguments_Removes_Node()
    {
        var doc = new DomDocument();
        var parent = doc.CreateElement("div");
        var a = doc.CreateElement("a");
        var target = doc.CreateElement("target");
        parent.AppendChild(a);
        parent.AppendChild(target);

        target.ReplaceWith();

        Assert.Equal([a], parent.ChildNodes);
        Assert.Null(target.ParentNode);
    }

    [Fact(Timeout = 600000)]
    public void Prepend_Inserts_At_Beginning()
    {
        var doc = new DomDocument();
        var parent = doc.CreateElement("div");
        var c = doc.CreateElement("c");
        parent.AppendChild(c);

        var a = doc.CreateElement("a");
        var b = doc.CreateElement("b");
        parent.Prepend(a, b);

        Assert.Equal([a, b, c], parent.ChildNodes);
    }

    [Fact(Timeout = 600000)]
    public void Append_Inserts_At_End()
    {
        var doc = new DomDocument();
        var parent = doc.CreateElement("div");
        var a = doc.CreateElement("a");
        parent.AppendChild(a);

        var b = doc.CreateElement("b");
        var c = doc.CreateElement("c");
        parent.Append(b, c);

        Assert.Equal([a, b, c], parent.ChildNodes);
    }

    [Fact(Timeout = 600000)]
    public void ReplaceChildren_Empties_Or_Replaces_All()
    {
        var doc = new DomDocument();
        var parent = doc.CreateElement("div");
        var a = doc.CreateElement("a");
        var b = doc.CreateElement("b");
        parent.AppendChild(a);
        parent.AppendChild(b);

        var x = doc.CreateElement("x");
        var y = doc.CreateElement("y");

        DomMutationRecord? mutation = null;
        doc.Mutated += m => mutation = m;

        parent.ReplaceChildren(x, y);

        Assert.Equal([x, y], parent.ChildNodes);
        Assert.Null(a.ParentNode);
        Assert.Null(b.ParentNode);
        Assert.NotNull(mutation);
        Assert.Equal(DomMutationType.ChildList, mutation.Type);

        parent.ReplaceChildren();
        Assert.Empty(parent.ChildNodes);
        Assert.Null(x.ParentNode);
        Assert.Null(y.ParentNode);
    }

    [Fact(Timeout = 600000)]
    public void ReplaceChildren_With_Existing_Child_Preserves_It()
    {
        var doc = new DomDocument();
        var parent = doc.CreateElement("div");
        var a = doc.CreateElement("a");
        var b = doc.CreateElement("b");
        var c = doc.CreateElement("c");
        parent.AppendChild(a);
        parent.AppendChild(b);
        parent.AppendChild(c);

        var x = doc.CreateElement("x");
        parent.ReplaceChildren(b, x);

        Assert.Equal([b, x], parent.ChildNodes);
        Assert.Same(parent, b.ParentNode);
        Assert.Null(a.ParentNode);
        Assert.Null(c.ParentNode);
    }

    [Fact(Timeout = 600000)]
    public void DocumentFragment_Unrolls_In_Mutations()
    {
        var doc = new DomDocument();
        var parent = doc.CreateElement("div");
        var frag = doc.CreateDocumentFragment();
        var f1 = doc.CreateElement("span");
        var f2 = doc.CreateElement("em");
        frag.AppendChild(f1);
        frag.AppendChild(f2);

        parent.Append(frag);
        Assert.Equal([f1, f2], parent.ChildNodes);
        Assert.Empty(frag.ChildNodes);
    }
}
