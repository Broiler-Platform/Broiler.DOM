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

    /// <summary>
    /// A template assembled through the node API, rather than by parsing, keeps whatever is appended
    /// to it in its own child list — and that list is not what it serializes. §13.3 says to serialize
    /// the template's <em>contents</em>, so children put on the element itself are invisible to
    /// <see cref="HtmlSerializer"/>. That surprises anyone who builds a template by hand, which is why
    /// it is pinned here rather than left to be discovered.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Hand_Built_Templates_Own_Children_Are_Not_What_It_Serializes()
    {
        var document = new DomDocument();
        var template = document.CreateElement("template");
        template.AppendChild(document.CreateTextNode("appended"));

        // The node API did what it was asked: the child is on the element.
        Assert.Single(template.ChildNodes);
        Assert.Empty(template.TemplateContents!.ChildNodes);

        // Serialization reads the contents, which nothing has written to.
        Assert.Equal("<template></template>", HtmlSerializer.Serialize(template));
    }

    /// <summary>
    /// The same rule seen from the other side: assigning <c>TextContent</c> replaces the element's
    /// children and leaves the contents alone, so a parsed template keeps serializing what it was
    /// parsed with. The assignment is not lost — it is simply not the thing that round-trips.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Setting_TextContent_On_A_Template_Does_Not_Change_What_It_Serializes()
    {
        var result = HtmlDocumentParser.ParseDocument("<body><template><li>row</li></template></body>");
        var template = result.Document.Body!.ChildNodes.OfType<DomElement>().Single();

        template.TextContent = "replaced";

        Assert.Equal("replaced", string.Concat(template.ChildNodes.Select(static node => node.TextContent)));
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

    /// <summary>
    /// HTML §13.2.6.1: when the last template on the stack of open elements is below the last
    /// table, foster parenting puts the node in the template's contents, not beside the table.
    /// </summary>
    /// <remarks>
    /// The table inside a template has a fragment for a parent, not an element, so a foster parent
    /// derived from the table's parent alone falls back to the body — and content the Standard
    /// calls inert reappears in the document tree, which is the whole thing this model prevents.
    /// </remarks>
    [Fact(Timeout = 600000)]
    public void Foster_Parenting_Inside_A_Template_Stays_In_The_Template()
    {
        var result = HtmlDocumentParser.ParseDocument(
            "<body><template><table><div id=fostered>x</div></table></template></body>");

        var template = result.Document.Body!.ChildNodes.OfType<DomElement>().Single();
        Assert.Equal("template", template.LocalName);
        Assert.Null(result.Document.GetElementById("fostered"));
        Assert.Empty(result.Document.GetElementsByTagName("div"));

        var fostered = template.TemplateContents!.ChildNodes.OfType<DomElement>()
            .Single(element => element.LocalName == "div");
        Assert.Equal("x", fostered.TextContent);
    }

    [Fact(Timeout = 600000)]
    public void Foster_Parented_Text_Inside_A_Template_Stays_In_The_Template()
    {
        var result = HtmlDocumentParser.ParseDocument(
            "<body><template><table>stray</table></template></body>");

        var template = Assert.IsType<DomElement>(Assert.Single(result.Document.Body!.ChildNodes));
        Assert.Equal("template", template.LocalName);
        Assert.Equal("stray", template.TemplateContents!.TextContent);
    }
}
