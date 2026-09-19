using System;
using System.Collections.Generic;

namespace Broiler.Dom.Tests;

public sealed class DomCharacterDataTests
{
    [Fact(Timeout = 600000)]
    public void Length_Returns_Text_Length()
    {
        var doc = new DomDocument();
        var text = doc.CreateTextNode("hello");
        Assert.Equal(5, text.Length);

        var empty = doc.CreateTextNode(string.Empty);
        Assert.Equal(0, empty.Length);

        var comment = doc.CreateComment("a comment");
        Assert.Equal(9, comment.Length);
    }

    [Fact(Timeout = 600000)]
    public void SubstringData_Extracts_Range_And_Clamps_Count()
    {
        var doc = new DomDocument();
        var text = doc.CreateTextNode("abcdef");

        Assert.Equal("bcd", text.SubstringData(1, 3));
        Assert.Equal("cdef", text.SubstringData(2, 100)); // clamped
        Assert.Equal(string.Empty, text.SubstringData(6, 0));
        Assert.Equal(string.Empty, text.SubstringData(6, 10));

        var ex1 = Assert.Throws<DomException>(() => text.SubstringData(-1, 2));
        Assert.Equal("IndexSizeError", ex1.Name);

        var ex2 = Assert.Throws<DomException>(() => text.SubstringData(7, 1));
        Assert.Equal("IndexSizeError", ex2.Name);

        var ex3 = Assert.Throws<DomException>(() => text.SubstringData(2, -1));
        Assert.Equal("IndexSizeError", ex3.Name);
    }

    [Fact(Timeout = 600000)]
    public void AppendData_Adds_To_End_And_Fires_Mutation()
    {
        var doc = new DomDocument();
        var text = doc.CreateTextNode("hello");
        DomMutationRecord? lastMutation = null;
        doc.Mutated += m => lastMutation = m;

        text.AppendData(" world");

        Assert.Equal("hello world", text.Data);
        Assert.Equal(11, text.Length);
        Assert.NotNull(lastMutation);
        Assert.Equal(DomMutationType.CharacterData, lastMutation.Type);
        Assert.Equal("hello", lastMutation.OldValue);
        Assert.Equal("hello world", lastMutation.NewValue);
    }

    [Fact(Timeout = 600000)]
    public void InsertData_Inserts_At_Offset()
    {
        var doc = new DomDocument();
        var text = doc.CreateTextNode("heo");

        text.InsertData(2, "ll");
        Assert.Equal("hello", text.Data);

        text.InsertData(0, "prefix-");
        Assert.Equal("prefix-hello", text.Data);

        text.InsertData(text.Length, "!");
        Assert.Equal("prefix-hello!", text.Data);

        var ex = Assert.Throws<DomException>(() => text.InsertData(100, "oops"));
        Assert.Equal("IndexSizeError", ex.Name);
    }

    [Fact(Timeout = 600000)]
    public void DeleteData_Removes_Range_And_Clamps_Count()
    {
        var doc = new DomDocument();
        var text = doc.CreateTextNode("0123456789");

        text.DeleteData(2, 3);
        Assert.Equal("0156789", text.Data);

        text.DeleteData(5, 100); // clamped to end
        Assert.Equal("01567", text.Data);

        var ex = Assert.Throws<DomException>(() => text.DeleteData(-1, 2));
        Assert.Equal("IndexSizeError", ex.Name);
    }

    [Fact(Timeout = 600000)]
    public void ReplaceData_Substitutes_Content()
    {
        var doc = new DomDocument();
        var text = doc.CreateTextNode("the quick fox");

        text.ReplaceData(10, 3, "brown fox");
        Assert.Equal("the quick brown fox", text.Data);

        text.ReplaceData(0, 3, "A");
        Assert.Equal("A quick brown fox", text.Data);

        text.ReplaceData(8, 100, "rabbit"); // clamped count
        Assert.Equal("A quick rabbit", text.Data);
    }

    [Fact(Timeout = 600000)]
    public void SplitText_Standalone_Splits_Data_And_Returns_New_Node()
    {
        var doc = new DomDocument();
        var text = doc.CreateTextNode("HelloWorld");

        var second = text.SplitText(5);

        Assert.Equal("Hello", text.Data);
        Assert.Equal("World", second.Data);
        Assert.Null(text.ParentNode);
        Assert.Null(second.ParentNode);
        Assert.Same(doc, second.OwnerDocument);
    }

    [Fact(Timeout = 600000)]
    public void SplitText_Connected_Inserts_Second_Node_As_Next_Sibling()
    {
        var doc = new DomDocument();
        var parent = doc.CreateElement("div");
        var before = doc.CreateElement("span");
        var text = doc.CreateTextNode("split me");
        var after = doc.CreateElement("b");

        parent.AppendChild(before);
        parent.AppendChild(text);
        parent.AppendChild(after);

        var second = text.SplitText(5);

        Assert.Equal("split", text.Data);
        Assert.Equal(" me", second.Data);
        Assert.Equal(4, parent.ChildNodes.Count);
        Assert.Equal([before, text, second, after], parent.ChildNodes);
        Assert.Same(second, text.NextSibling);
        Assert.Same(text, second.PreviousSibling);
        Assert.Same(after, second.NextSibling);
        Assert.Same(second, after.PreviousSibling);
    }

    [Fact(Timeout = 600000)]
    public void SplitText_At_Edges_Handles_Zero_And_Length()
    {
        var doc = new DomDocument();
        var text = doc.CreateTextNode("edge");

        var atZero = text.SplitText(0);
        Assert.Equal(string.Empty, text.Data);
        Assert.Equal("edge", atZero.Data);

        var atEnd = atZero.SplitText(4);
        Assert.Equal("edge", atZero.Data);
        Assert.Equal(string.Empty, atEnd.Data);
    }

    [Fact(Timeout = 600000)]
    public void SplitText_Throws_On_Out_Of_Bounds()
    {
        var doc = new DomDocument();
        var text = doc.CreateTextNode("bounds");

        var ex1 = Assert.Throws<DomException>(() => text.SplitText(-1));
        Assert.Equal("IndexSizeError", ex1.Name);

        var ex2 = Assert.Throws<DomException>(() => text.SplitText(7));
        Assert.Equal("IndexSizeError", ex2.Name);
    }
}
