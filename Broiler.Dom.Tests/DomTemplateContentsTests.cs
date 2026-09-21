namespace Broiler.Dom.Tests;

/// <summary>
/// The template contents half of HTML §4.12.3 that lives in the kernel: which elements have a
/// contents fragment, and that a copy of a template carries its contents rather than losing them
/// to a child walk that never sees them.
/// </summary>
public sealed class DomTemplateContentsTests
{
    [Fact(Timeout = 600000)]
    public void A_Template_Element_Is_Created_With_An_Empty_Contents_Fragment()
    {
        var document = new DomDocument();
        var template = document.CreateElement("template");

        Assert.NotNull(template.TemplateContents);
        Assert.Empty(template.TemplateContents!.ChildNodes);
        Assert.Same(document, template.TemplateContents.OwnerDocument);
    }

    [Fact(Timeout = 600000)]
    public void The_Contents_Fragment_Has_A_Stable_Identity()
    {
        var document = new DomDocument();
        var template = document.CreateElement("template");

        Assert.Same(template.TemplateContents, template.TemplateContents);
    }

    [Fact(Timeout = 600000)]
    public void Only_An_Html_Template_Has_Contents()
    {
        var document = new DomDocument();

        Assert.Null(document.CreateElement("div").TemplateContents);
        Assert.NotNull(document.CreateElementNS(DomNamespaces.Html, "template").TemplateContents);
        Assert.Null(document.CreateElementNS(DomNamespaces.Svg, "template").TemplateContents);
    }

    [Fact(Timeout = 600000)]
    public void A_Templates_Contents_Are_Not_Its_Children()
    {
        var document = new DomDocument();
        var template = document.CreateElement("template");
        document.AppendChild(template);
        template.TemplateContents!.AppendChild(document.CreateElement("span"));

        Assert.Empty(template.ChildNodes);
        Assert.Empty(document.GetElementsByTagName("span"));
        Assert.Equal(string.Empty, template.TextContent);
    }

    [Fact(Timeout = 600000)]
    public void A_Deep_Clone_Copies_The_Contents()
    {
        var document = new DomDocument();
        var template = document.CreateElement("template");
        var span = document.CreateElement("span");
        span.AppendChild(document.CreateTextNode("a"));
        template.TemplateContents!.AppendChild(span);

        var clone = (DomElement)template.CloneNode(deep: true);

        Assert.NotSame(template.TemplateContents, clone.TemplateContents);
        Assert.Equal("a", clone.TemplateContents!.TextContent);
        Assert.NotSame(span, clone.TemplateContents.ChildNodes[0]);
    }

    [Fact(Timeout = 600000)]
    public void A_Shallow_Clone_Leaves_The_Contents_Empty()
    {
        var document = new DomDocument();
        var template = document.CreateElement("template");
        template.TemplateContents!.AppendChild(document.CreateElement("span"));

        var clone = (DomElement)template.CloneNode();

        Assert.NotNull(clone.TemplateContents);
        Assert.Empty(clone.TemplateContents!.ChildNodes);
    }

    [Fact(Timeout = 600000)]
    public void A_Deep_Import_Carries_The_Contents_Into_The_Target_Document()
    {
        var source = new DomDocument();
        var template = source.CreateElement("template");
        template.TemplateContents!.AppendChild(source.CreateTextNode("a"));

        var target = new DomDocument();
        var imported = (DomElement)target.ImportNode(template, deep: true);

        Assert.Equal("a", imported.TemplateContents!.TextContent);
        Assert.Same(target, imported.TemplateContents.OwnerDocument);
    }

    /// <summary>
    /// HTML §4.12.3's adopting steps move a template's contents with it. The contents are not
    /// children, so the walk behind <c>adoptNode</c> does not reach them on its own.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Adopting_A_Template_Takes_Its_Contents_To_The_New_Document()
    {
        var source = new DomDocument();
        var template = source.CreateElement("template");
        var span = source.CreateElement("span");
        template.TemplateContents!.AppendChild(span);

        var target = new DomDocument();
        target.AdoptNode(template);

        Assert.Same(target, template.OwnerDocument);
        Assert.Same(target, template.TemplateContents.OwnerDocument);
        Assert.Same(target, span.OwnerDocument);
    }
}
