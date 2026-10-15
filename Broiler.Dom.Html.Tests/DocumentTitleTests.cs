using System.Linq;
using Broiler.Dom;
using Broiler.Dom.Html;
using Xunit;

namespace Broiler.Dom.Html.Tests;

/// <summary>
/// The document's title is the first HTML title element's text (HTML §4.2.2, the title element;
/// <c>document.title</c> reads it), and an SVG or MathML <c>&lt;title&gt;</c> is an ordinary foreign
/// element where it stands (§13.2.6.5). An HTML title met before the body goes in the head, and one
/// met after it — and every one in a fragment whose context is not the head or html element — stays
/// where it is written: "in body" processes it with the "in head" rules, which insert it at the
/// current node (§13.2.6.4.7).
/// </summary>
/// <remarks>
/// The tree builder took every <c>&lt;title&gt;</c> outside a template for the document's: it moved
/// an inline icon's SVG title, its tooltip and accessible name, out of the <c>&lt;svg&gt;</c> into
/// the head, and appended the text of every title to the result's <c>Title</c> — so a page titled
/// "Page" with an icon titled "Icon" was "PageIcon", where a browser shows "Page". Once foreign
/// content kept the SVG title in its svg, every HTML title still went to the head, so
/// <c>div.innerHTML = '&lt;title&gt;t&lt;/title&gt;x'</c> gave just <c>x</c>: the title was moved into
/// the head of the document the fragment is parsed in, and lost with it.
/// </remarks>
public sealed class DocumentTitleTests
{
    private static readonly DomDocument Owner = new();

    private static HtmlDocumentParseResult Parse(string html) =>
        HtmlDocumentParser.ParseDocument("<!DOCTYPE html>" + html);

    [Fact(Timeout = 600000)]
    public void An_Svg_Title_Stays_In_Its_Svg_And_Is_Not_The_Documents_Title()
    {
        var result = Parse("<title>Page</title><p>An icon <svg><title>Icon</title><path d='M0 0'/></svg></p>");

        Assert.Equal("Page", result.Title);
        Assert.Equal("<head><title>Page</title></head>", HtmlSerializer.Serialize(result.Document.Head!));
        Assert.Equal(
            "<body><p>An icon <svg><title>Icon</title><path d=\"M0 0\"></path></svg></p></body>",
            HtmlSerializer.Serialize(result.Document.Body!));
    }

    [Fact(Timeout = 600000)]
    public void A_MathML_Title_Stays_In_Its_Math()
    {
        var result = Parse("<p><math><title>formula</title><mi>x</mi></math></p>");

        Assert.Equal(string.Empty, result.Title);
        Assert.Equal("<head></head>", HtmlSerializer.Serialize(result.Document.Head!));
        Assert.Equal("<body><p><math><title>formula</title><mi>x</mi></math></p></body>", HtmlSerializer.Serialize(result.Document.Body!));
    }

    /// <summary>A self-closing SVG title is acknowledged, as in foreign content it is: it opens nothing.</summary>
    [Fact(Timeout = 600000)]
    public void A_Self_Closing_Svg_Title_Opens_Nothing()
    {
        var result = Parse("<svg><title/><circle r='1'/></svg><p>x</p>");

        Assert.Equal(string.Empty, result.Title);
        Assert.Equal(
            "<body><svg><title></title><circle r=\"1\"></circle></svg><p>x</p></body>",
            HtmlSerializer.Serialize(result.Document.Body!));
    }

    [Theory(Timeout = 600000)]
    // An icon ahead of the page's own title does not take its place.
    [InlineData("<svg><title>Icon</title></svg><title>Page</title>", "Page")]
    // Only the first HTML title element counts, even an empty one.
    [InlineData("<title>A</title><title>B</title>", "A")]
    [InlineData("<title></title><title>B</title>", "")]
    [InlineData("<title>  spaced  </title>", "spaced")]
    // An HTML integration point is HTML content again: a title there is an HTML title element.
    [InlineData("<svg><foreignObject><title>Inside</title></foreignObject></svg>", "Inside")]
    [InlineData("<svg><desc><title>Described</title></desc></svg>", "Described")]
    // A template's title is inert, as before.
    [InlineData("<template><title>Inert</title></template><title>Real</title>", "Real")]
    public void The_Title_Is_The_First_Html_Title_Elements_Text(string html, string title) =>
        Assert.Equal(title, Parse(html).Title);

    [Fact(Timeout = 600000)]
    public void A_Title_In_The_Body_Stays_In_The_Body_And_Is_The_Documents_Title()
    {
        var result = Parse("<body><title>t</title><p>x</p>");

        Assert.Equal("t", result.Title);
        Assert.Equal("<head></head>", HtmlSerializer.Serialize(result.Document.Head!));
        Assert.Equal("<body><title>t</title><p>x</p></body>", HtmlSerializer.Serialize(result.Document.Body!));
    }

    [Theory(Timeout = 600000)]
    // Before the body, a title goes in the head — after </head> too ("after head" puts the head back
    // on the stack for it) — and the first one is the document's.
    [InlineData("<title>A</title><p>x</p><title>B</title>", "<head><title>A</title></head>", "<body><p>x</p><title>B</title></body>")]
    [InlineData("<head></head><title>A</title><body><p>x</p>", "<head><title>A</title></head>", "<body><p>x</p></body>")]
    // After the body has started, it is inserted at the current node, whatever that is.
    [InlineData("<p>a<title>A</title>b</p>", "<head></head>", "<body><p>a<title>A</title>b</p></body>")]
    [InlineData("x<title>A</title>", "<head></head>", "<body>x<title>A</title></body>")]
    [InlineData("<body>x</body><title>A</title>", "<head></head>", "<body>x<title>A</title></body>")]
    // An HTML integration point is HTML content: the title is an HTML title element inside it.
    [InlineData("<svg><foreignObject><title>A</title></foreignObject></svg>", "<head></head>",
        "<body><svg><foreignObject><title>A</title></foreignObject></svg></body>")]
    [InlineData("<math><mi><title>A</title></mi></math>", "<head></head>", "<body><math><mi><title>A</title></mi></math></body>")]
    // An HTML title start tag is not acknowledged as self-closing: the title stays open for its text.
    [InlineData("<p>a<title/>A</title>b</p>", "<head></head>", "<body><p>a<title>A</title>b</p></body>")]
    public void A_Title_Goes_In_The_Head_Only_Before_The_Body(string html, string head, string body)
    {
        var result = Parse(html);

        Assert.Equal("A", result.Title);
        Assert.Equal(head, HtmlSerializer.Serialize(result.Document.Head!));
        Assert.Equal(body, HtmlSerializer.Serialize(result.Document.Body!));
        Assert.All(
            result.Document.GetElementsByTagName("title"),
            title => Assert.Equal(DomNamespaces.Html, title.NamespaceUri));
    }

    /// <summary>
    /// A title written in a table outside a cell is fostered out of it, as any other content is
    /// ("in table", "anything else"), never moved to the head.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Title_In_A_Table_Is_Fostered_Out_Of_It()
    {
        var result = Parse("<table><title>t</title><tr><td>c</td></tr></table>");

        var title = Assert.Single(result.Document.GetElementsByTagName("title"));
        Assert.Same(result.Document.Body, title.ParentNode);
        Assert.Equal("<head></head>", HtmlSerializer.Serialize(result.Document.Head!));
        Assert.Equal("t", result.Title);
    }

    /// <summary>
    /// <c>Title</c> is read from the first HTML title element in tree order, which is not always the
    /// first one the parse meets: a fostered title is placed outside the table it was written in.
    /// </summary>
    [Theory(Timeout = 600000)]
    [InlineData("<title>A</title><p><title>B</title>")]
    [InlineData("<p><title>A</title><svg><foreignObject><title>B</title></foreignObject></svg>")]
    [InlineData("<table><title>A</title><tr><td><title>B</title></td></tr></table>")]
    [InlineData("<table><tr><td><title>A</title></td></tr><title>B</title></table>")]
    public void The_Title_Is_The_First_Html_Title_Element_In_Tree_Order(string html)
    {
        var result = Parse(html);

        var first = result.Document.Descendants().OfType<DomElement>()
            .First(element => element.NamespaceUri == DomNamespaces.Html && element.LocalName == "title");
        Assert.Equal(first.TextContent, result.Title);
    }

    [Theory(Timeout = 600000)]
    [InlineData("div")]
    [InlineData("body")]
    [InlineData("p")]
    [InlineData("td")]
    [InlineData("template")]
    public void A_Title_In_A_Fragment_Stays_In_The_Fragment(string context) =>
        Assert.Equal(
            "<title>t</title>x",
            HtmlSerializer.Serialize(HtmlDocumentParser.ParseFragment("<title>t</title>x", context).Fragment));

    [Fact(Timeout = 600000)]
    public void A_Title_In_A_Fragment_Stays_Where_It_Is_Written() =>
        Assert.Equal(
            "<p>a<title>t</title>b</p><ul><li><title>u</title></li></ul>",
            HtmlSerializer.Serialize(HtmlDocumentParser.ParseFragment(
                "<p>a<title>t</title>b</p><ul><li><title>u</title></li></ul>", "div").Fragment));

    /// <summary>
    /// An html context starts "before head" (§13.4 resets the insertion mode from it), so a title
    /// ahead of any content still opens the head — in the fragment, which holds both head and body.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Title_In_An_Html_Fragment_Goes_In_Its_Head() =>
        Assert.Equal(
            "<head><title>t</title></head><body>x</body>",
            HtmlSerializer.Serialize(HtmlDocumentParser.ParseFragment("<title>t</title>x", "html").Fragment));

    [Theory(Timeout = 600000)]
    [InlineData(DomNamespaces.Svg, "foreignObject")]
    [InlineData(DomNamespaces.Svg, "desc")]
    [InlineData(DomNamespaces.Svg, "title")]
    [InlineData(DomNamespaces.MathMl, "mi")]
    public void A_Title_In_A_Fragment_At_An_Integration_Point_Is_An_Html_Title_There(string ns, string context)
    {
        var fragment = HtmlDocumentParser.ParseFragment("<title>t</title>x", Owner.CreateElementNS(ns, context)).Fragment;

        Assert.Equal("<title>t</title>x", HtmlSerializer.Serialize(fragment));
        Assert.Equal(DomNamespaces.Html, Assert.IsType<DomElement>(fragment.FirstChild).NamespaceUri);
    }

    [Fact(Timeout = 600000)]
    public void A_Title_In_An_Svg_Fragment_Stays_An_Svg_Title()
    {
        var fragment = HtmlDocumentParser.ParseFragment("<title>t</title><rect/>", Owner.CreateElementNS(DomNamespaces.Svg, "g")).Fragment;

        var title = Assert.IsType<DomElement>(fragment.FirstChild);
        Assert.Equal(DomNamespaces.Svg, title.NamespaceUri);
        Assert.Equal("<title>t</title><rect></rect>", HtmlSerializer.Serialize(fragment));
    }

    /// <summary>The bridge's innerHTML, outerHTML and insertAdjacentHTML go through this overload.</summary>
    [Fact(Timeout = 600000)]
    public void TryBuildFragment_Keeps_A_Title()
    {
        Assert.True(HtmlFragmentParsing.TryBuildFragment(Owner.CreateElement("div"), "<title>t</title>x", out var inDiv));
        Assert.Equal("<title>t</title>x", HtmlSerializer.Serialize(inDiv));

        Assert.True(HtmlFragmentParsing.TryBuildFragment(
            Owner.CreateElementNS(DomNamespaces.Svg, "foreignObject"), "<title>t</title>x", out var inForeignObject));
        Assert.Equal("<title>t</title>x", HtmlSerializer.Serialize(inForeignObject));
    }
}
