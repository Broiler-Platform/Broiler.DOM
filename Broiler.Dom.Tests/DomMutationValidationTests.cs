namespace Broiler.Dom.Tests;

public sealed class DomMutationValidationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Rejected_Replacement_Preserves_Children_Indexes_And_Mutations(bool cycle)
    {
        var document = new DomDocument();
        var parent = document.CreateElement("div");
        var child = document.CreateElement("span");
        child.Id = "original";
        document.AppendChild(parent);
        parent.AppendChild(child);
        var version = document.Version;
        var mutations = new List<DomMutationRecord>();
        document.Mutated += mutations.Add;

        var error = Assert.Throws<DomException>(() => parent.ReplaceChild(cycle ? parent : document, child));

        Assert.Equal("HierarchyRequestError", error.Name);
        Assert.Same(child, Assert.Single(parent.ChildNodes));
        Assert.Same(parent, child.ParentNode);
        Assert.Same(child, document.GetElementById("original"));
        Assert.Equal(version, document.Version);
        Assert.Empty(mutations);
    }

    [Fact]
    public void Rejected_Document_Fragment_Replacement_Preserves_Both_Trees()
    {
        var document = new DomDocument();
        var root = document.CreateElement("html");
        document.AppendChild(root);
        var otherDocument = new DomDocument();
        var fragment = otherDocument.CreateDocumentFragment();
        fragment.AppendChild(otherDocument.CreateElement("one"));
        fragment.AppendChild(otherDocument.CreateElement("two"));
        var version = document.Version;
        var otherVersion = otherDocument.Version;

        Assert.Throws<DomException>(() => document.ReplaceChild(fragment, root));

        Assert.Same(root, document.DocumentElement);
        Assert.Equal(2, fragment.ChildNodes.Count);
        Assert.All(fragment.ChildNodes, child => Assert.Same(otherDocument, child.OwnerDocument));
        Assert.Equal(version, document.Version);
        Assert.Equal(otherVersion, otherDocument.Version);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Document_Root_Can_Be_Replaced_With_Element_Or_Fragment(bool useFragment)
    {
        var document = new DomDocument();
        var doctype = document.CreateDocumentType("html");
        var oldRoot = document.CreateElement("html");
        var newRoot = document.CreateElement("html");
        document.AppendChild(doctype);
        document.AppendChild(oldRoot);
        DomNode replacement = newRoot;
        if (useFragment)
        {
            replacement = document.CreateDocumentFragment();
            replacement.AppendChild(newRoot);
        }

        Assert.Same(oldRoot, document.ReplaceChild(replacement, oldRoot));

        Assert.Null(oldRoot.ParentNode);
        Assert.Same(newRoot, document.DocumentElement);
        Assert.Equal(new DomNode[] { doctype, newRoot }, document.ChildNodes);
    }

    [Fact]
    public void Replacement_With_Next_Sibling_Keeps_The_Remaining_Order()
    {
        var document = new DomDocument();
        var parent = document.CreateElement("div");
        var first = document.CreateElement("a");
        var second = document.CreateElement("b");
        var third = document.CreateElement("c");
        parent.AppendChild(first);
        parent.AppendChild(second);
        parent.AppendChild(third);

        parent.ReplaceChild(second, first);

        Assert.Equal(new DomNode[] { second, third }, parent.ChildNodes);
        Assert.Null(first.ParentNode);
    }

    [Fact]
    public void Document_Order_Is_Checked_Before_Moving_Or_Replacing_A_Child()
    {
        var document = new DomDocument();
        var doctype = document.CreateDocumentType("html");
        var root = document.CreateElement("html");
        var comment = document.CreateComment("tail");
        document.AppendChild(doctype);
        document.AppendChild(root);
        document.AppendChild(comment);
        var version = document.Version;

        Assert.Throws<DomException>(() => document.AppendChild(doctype));
        Assert.Throws<DomException>(() => document.InsertBefore(root, doctype));
        Assert.Throws<DomException>(() => document.ReplaceChild(doctype, comment));

        Assert.Equal(new DomNode[] { doctype, root, comment }, document.ChildNodes);
        Assert.Equal(version, document.Version);
    }
}
