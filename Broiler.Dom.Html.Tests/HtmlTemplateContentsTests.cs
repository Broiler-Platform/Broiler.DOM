using System.Linq;
using Broiler.Dom;
using Xunit;

namespace Broiler.Dom.Html.Tests;

/// <summary>
/// HTML §4.12.3: a <c>&lt;template&gt;</c> element's children are its <em>template contents</em>,
/// a separate <c>DocumentFragment</c> that is not part of the element's child list, and §13.2.6.1
/// is where the tree builder redirects insertions into it.
/// </summary>
/// <remarks>
/// The queries below are one model expressed twice: what the tree holds, and what every walk over
/// that tree therefore stops seeing. While template children sat in the child list each of them
/// answered with markup the Standard calls inert, and only the token-scanning base-href overload
/// had a guard of its own.
/// </remarks>
public sealed class HtmlTemplateContentsTests
{
    [Fact(Timeout = 600000)]
    public void Template_Children_Become_Template_Contents_And_Leave_The_Child_List_Empty()
    {
        var result = HtmlDocumentParser.ParseDocument("<body><template><div id=inner>x</div></template></body>");

        var template = result.Document.Body!.ChildNodes.OfType<DomElement>().Single();
        Assert.Equal("template", template.LocalName);
        Assert.Empty(template.ChildNodes);

        var contents = template.TemplateContents;
        Assert.NotNull(contents);
        var inner = contents!.ChildNodes.OfType<DomElement>().Single();
        Assert.Equal("div", inner.LocalName);
        Assert.Equal("x", inner.TextContent);
    }

    [Fact(Timeout = 600000)]
    public void Nested_Templates_Each_Hold_Their_Own_Contents()
    {
        var result = HtmlDocumentParser.ParseDocument(
            "<body><template><p>outer</p><template><p>inner</p></template></template></body>");

        var outer = result.Document.Body!.ChildNodes.OfType<DomElement>().Single();
        var outerContents = outer.TemplateContents;
        Assert.NotNull(outerContents);
        Assert.Equal(
            new[] { "p", "template" },
            outerContents!.ChildNodes.OfType<DomElement>().Select(element => element.LocalName).ToArray());

        var inner = outerContents.ChildNodes.OfType<DomElement>().Last();
        Assert.Empty(inner.ChildNodes);
        Assert.Equal("inner", inner.TemplateContents!.TextContent);
    }

    [Fact(Timeout = 600000)]
    public void Text_And_Comments_Inside_A_Template_Are_Diverted_Too()
    {
        var result = HtmlDocumentParser.ParseDocument("<body><template>text<!--note--></template></body>");

        var template = result.Document.Body!.ChildNodes.OfType<DomElement>().Single();
        Assert.Empty(template.ChildNodes);

        var contents = template.TemplateContents!;
        Assert.Equal(2, contents.ChildNodes.Count);
        Assert.Equal("text", contents.ChildNodes[0].TextContent);
        Assert.Equal(DomNodeType.Comment, contents.ChildNodes[1].NodeType);
    }

    [Fact(Timeout = 600000)]
    public void GetElementById_Does_Not_Reach_Into_Template_Contents()
    {
        var result = HtmlDocumentParser.ParseDocument(
            "<body><template><span id=hidden></span></template><span id=visible></span></body>");

        Assert.Null(result.Document.GetElementById("hidden"));
        Assert.NotNull(result.Document.GetElementById("visible"));
    }

    [Fact(Timeout = 600000)]
    public void GetElementsByTagName_Does_Not_Reach_Into_Template_Contents()
    {
        var result = HtmlDocumentParser.ParseDocument(
            "<body><template><span>inert</span></template><span>live</span></body>");

        var span = Assert.Single(result.Document.GetElementsByTagName("span"));
        Assert.Equal("live", span.TextContent);
    }

    [Fact(Timeout = 600000)]
    public void Both_GetEffectiveBaseHref_Overloads_Ignore_A_Base_Inside_A_Template()
    {
        const string html = "<body><template><base href=\"https://ignore-me.example/\"></template>" +
                            "<base href=\"https://example.com/path/\"></body>";

        var result = HtmlDocumentParser.ParseDocument(html);

        Assert.Equal("https://example.com/path/", HtmlDocumentQueries.GetEffectiveBaseHref(html));
        Assert.Equal("https://example.com/path/", HtmlDocumentQueries.GetEffectiveBaseHref(result.Document));
    }

    [Fact(Timeout = 600000)]
    public void Both_GetEffectiveBaseHref_Overloads_Return_Null_For_A_Template_Only_Base()
    {
        const string html = "<body><template><base href=\"https://ignore-me.example/\"></template></body>";

        var result = HtmlDocumentParser.ParseDocument(html);

        Assert.Null(HtmlDocumentQueries.GetEffectiveBaseHref(html));
        Assert.Null(HtmlDocumentQueries.GetEffectiveBaseHref(result.Document));
    }

    [Fact(Timeout = 600000)]
    public void FindMetaColorScheme_Ignores_A_Meta_Inside_A_Template()
    {
        var result = HtmlDocumentParser.ParseDocument(
            "<body><template><meta name=\"color-scheme\" content=\"dark\"></template></body>");

        Assert.Null(HtmlMetaScanner.FindMetaColorScheme(result.Document));
    }

    [Fact(Timeout = 600000)]
    public void GetFormElements_Ignores_Controls_Inside_A_Template()
    {
        var result = HtmlDocumentParser.ParseDocument(
            "<body><form><input name=live><template><input name=inert></template></form></body>");

        var form = result.Document.GetElementsByTagName("form").Single();
        var control = Assert.Single(HtmlFormQueries.GetFormElements(form));

        Assert.Equal("live", control.GetAttributeByQualifiedName("name"));
    }

    [Fact(Timeout = 600000)]
    public void A_Title_Inside_A_Template_Is_Not_The_Documents_Title()
    {
        var result = HtmlDocumentParser.ParseDocument(
            "<head><title>real</title></head><body><template><title>inert</title></template></body>");

        Assert.Equal("real", result.Title);
        Assert.Single(result.Document.GetElementsByTagName("title"));

        var template = result.Document.Body!.ChildNodes.OfType<DomElement>().Single();
        Assert.Equal("inert", template.TemplateContents!.TextContent);
    }

    [Fact(Timeout = 600000)]
    public void Serializing_A_Template_Emits_Its_Contents()
    {
        var result = HtmlDocumentParser.ParseDocument("<body><template><li>row</li></template></body>");
        var template = result.Document.Body!.ChildNodes.OfType<DomElement>().Single();

        Assert.Equal("<template><li>row</li></template>", HtmlSerializer.Serialize(template));
    }

    [Fact(Timeout = 600000)]
    public void Template_Markup_Round_Trips_Through_Parse_And_Serialize()
    {
        const string markup = "<template><p>a</p><template><span>b</span></template></template>";

        var result = HtmlDocumentParser.ParseDocument("<body>" + markup + "</body>");

        Assert.Equal(markup, HtmlSerializer.Serialize(result.Document.Body!.ChildNodes.OfType<DomElement>().Single()));
    }

    [Fact(Timeout = 600000)]
    public void ParseFragment_In_A_Template_Context_Returns_The_Contents()
    {
        var result = HtmlDocumentParser.ParseFragment("<tr><td>cell</td></tr>", "template");

        var row = result.Fragment.ChildNodes.OfType<DomElement>().Single();
        Assert.Equal("tr", row.LocalName);
    }

    [Fact(Timeout = 600000)]
    public void TryBuildFragment_In_A_Template_Context_Returns_The_Contents()
    {
        Assert.True(HtmlFragmentParsing.TryBuildFragment("template", "<span>a</span>", out var fragment));

        var span = fragment!.ChildNodes.OfType<DomElement>().Single();
        Assert.Equal("span", span.LocalName);
    }

    [Fact(Timeout = 600000)]
    public void A_Template_Nested_In_A_Table_Keeps_Its_Contents_Out_Of_The_Table()
    {
        var result = HtmlDocumentParser.ParseDocument(
            "<body><table><template><div>cell</div></template></table></body>");

        var table = result.Document.GetElementsByTagName("table").Single();
        var template = table.ChildNodes.OfType<DomElement>().Single();

        Assert.Equal("template", template.LocalName);
        Assert.Empty(result.Document.GetElementsByTagName("div"));
        Assert.Equal("cell", template.TemplateContents!.TextContent);
    }
}
