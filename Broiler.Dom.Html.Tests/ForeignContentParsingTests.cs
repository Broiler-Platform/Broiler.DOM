using System.Collections.Generic;
using System.Linq;
using Broiler.Dom;
using Broiler.Dom.Html;
using Xunit;

namespace Broiler.Dom.Html.Tests;

/// <summary>
/// Inline SVG and MathML are parsed as HTML §13.2.6.5 ("the rules for parsing tokens in foreign
/// content") and §13.2.6.1 ("insert a foreign element") build them: in the SVG and MathML
/// namespaces, with the SVG tag and attribute names in their camel case and the <c>xlink:</c>,
/// <c>xml:</c> and <c>xmlns</c> attributes in their namespaces. Integration points return their
/// content to HTML, the HTML start tags that cannot appear there close the foreign content, and the
/// tokenizer reads an SVG <c>style</c>, <c>title</c> or <c>script</c> as markup.
/// </summary>
/// <remarks>
/// The tree builder used to create every element through <c>DomDocument.CreateElement</c>: in the HTML
/// namespace and lowercased. <c>&lt;svg&gt;&lt;foreignObject&gt;</c> was an HTML
/// <c>foreignobject</c>, <c>viewBox</c> an attribute named <c>viewbox</c>, and an SVG
/// <c>&lt;style&gt;</c> an HTML raw-text element. Each expected tree here is the one a browser builds.
/// </remarks>
public sealed class ForeignContentParsingTests
{
    private const string Xlink = DomNamespaces.XLink;

    private static DomDocument Parse(string html) =>
        HtmlDocumentParser.ParseDocument("<!DOCTYPE html>" + html).Document;

    private static string Body(string html) => HtmlSerializer.Serialize(Parse(html).Body!);

    private static IEnumerable<DomElement> Elements(DomNode root) => root.Descendants().OfType<DomElement>();

    private static DomElement Element(DomDocument document, string localName) =>
        Elements(document).First(element => element.LocalName == localName);

    [Fact(Timeout = 600000)]
    public void Svg_And_Math_And_Their_Descendants_Are_In_Their_Namespaces()
    {
        var document = Parse("<p><svg><g><circle r='1'/></g></svg><math><mrow><mi>x</mi></mrow></math></p>");

        foreach (var name in new[] { "svg", "g", "circle" })
            Assert.Equal(DomNamespaces.Svg, Element(document, name).NamespaceUri);
        foreach (var name in new[] { "math", "mrow", "mi" })
            Assert.Equal(DomNamespaces.MathMl, Element(document, name).NamespaceUri);
        Assert.Equal(DomNamespaces.Html, Element(document, "p").NamespaceUri);
    }

    [Theory(Timeout = 600000)]
    [InlineData("foreignobject", "foreignObject")]
    [InlineData("LINEARGRADIENT", "linearGradient")]
    [InlineData("radialGradient", "radialGradient")]
    [InlineData("clippath", "clipPath")]
    [InlineData("fegaussianblur", "feGaussianBlur")]
    [InlineData("feDropShadow", "feDropShadow")]
    [InlineData("textpath", "textPath")]
    // A name the table does not list stays lowercase, as in a browser.
    [InlineData("myElement", "myelement")]
    public void Svg_Tag_Names_Take_Their_Case(string written, string localName)
    {
        var document = Parse($"<svg><{written}></{written}></svg>");

        var element = Assert.Single(Elements(document.Body!).Where(element => element.LocalName != "svg"));
        Assert.Equal(localName, element.LocalName);
        Assert.Equal(localName, element.TagName);
        Assert.Equal(DomNamespaces.Svg, element.NamespaceUri);
    }

    [Fact(Timeout = 600000)]
    public void A_MathML_Element_Keeps_The_Name_It_Was_Written_With_In_Lowercase()
    {
        // The SVG tag name table applies to SVG elements only.
        var document = Parse("<math><foreignObject></foreignObject></math>");

        var element = Element(document, "foreignobject");
        Assert.Equal(DomNamespaces.MathMl, element.NamespaceUri);
    }

    [Fact(Timeout = 600000)]
    public void Svg_Attribute_Names_Take_Their_Case()
    {
        var svg = Element(Parse("<svg VIEWBOX='0 0 10 10' preserveaspectratio='none' data-Name='x'></svg>"), "svg");

        Assert.Equal(["viewBox", "preserveAspectRatio", "data-name"], svg.AttributeQualifiedNames.ToArray());
        Assert.Equal("0 0 10 10", svg.GetAttribute("viewBox"));
        Assert.Equal("0 0 10 10", svg.GetAttributeNS(null, "viewBox"));
        Assert.True(svg.HasAttribute("preserveAspectRatio"));
        // A browser's getAttribute("viewbox") is null here; the name-based methods still find the
        // attribute by its lowercase spelling, as they did when the parser stored it lowercase.
        Assert.Equal("0 0 10 10", svg.GetAttribute("viewbox"));
    }

    [Fact(Timeout = 600000)]
    public void The_MathML_DefinitionUrl_Attribute_Takes_Its_Case()
    {
        var document = Parse("<math definitionurl='a'><mi definitionURL='b' viewbox='c'>x</mi></math>");

        Assert.Equal("a", Element(document, "math").GetAttribute("definitionURL"));
        var mi = Element(document, "mi");
        Assert.Equal("b", mi.GetAttribute("definitionURL"));
        // The SVG attribute table applies to SVG elements only.
        Assert.Equal("c", mi.GetAttribute("viewbox"));
    }

    [Fact(Timeout = 600000)]
    public void Foreign_Attributes_Are_In_Their_Namespaces()
    {
        var document = Parse(
            "<svg xmlns='http://www.w3.org/2000/svg' xmlns:xlink='http://www.w3.org/1999/xlink'>" +
            "<use xlink:href='#a' xml:lang='en' xml:space='preserve' xlink:title='t'/></svg>");
        var svg = Element(document, "svg");
        var use = Element(document, "use");

        var href = Assert.Single(use.Attributes.Values, attribute => attribute.LocalName == "href");
        Assert.Equal(Xlink, href.NamespaceUri);
        Assert.Equal("xlink", href.Name.Prefix);
        Assert.Equal("xlink:href", href.QualifiedName);
        Assert.Equal("#a", use.GetAttributeNS(Xlink, "href"));
        Assert.Equal("t", use.GetAttributeNS(Xlink, "title"));
        Assert.Equal("en", use.GetAttributeNS(DomNamespaces.Xml, "lang"));
        Assert.Equal("preserve", use.GetAttributeNS(DomNamespaces.Xml, "space"));
        // The name-based methods find a namespaced attribute by its qualified name.
        Assert.Equal("#a", use.GetAttribute("xlink:href"));

        Assert.Equal(DomNamespaces.Svg, svg.GetAttributeNS(DomNamespaces.Xmlns, "xmlns"));
        Assert.Equal(Xlink, svg.GetAttributeNS(DomNamespaces.Xmlns, "xlink"));
    }

    [Fact(Timeout = 600000)]
    public void An_Unlisted_Prefixed_Attribute_Stays_Whole_And_In_No_Namespace()
    {
        var use = Element(Parse("<svg><use foo:bar='1' xlink:unknown='2'/></svg>"), "use");

        Assert.Equal("1", use.GetAttributeNS(null, "foo:bar"));
        Assert.Equal("2", use.GetAttributeNS(null, "xlink:unknown"));
    }

    [Fact(Timeout = 600000)]
    public void A_Foreign_Element_Name_With_Colons_Is_Kept_Whole()
    {
        var element = Elements(Parse("<svg><a:b:c></a:b:c></svg>").Body!).Last();

        Assert.Equal("a:b:c", element.LocalName);
        Assert.Null(element.Prefix);
        Assert.Equal(DomNamespaces.Svg, element.NamespaceUri);
    }

    [Theory(Timeout = 600000)]
    // HTML integration points: SVG foreignObject, desc and title, and an HTML annotation-xml.
    [InlineData("<svg><foreignObject><div>x</div></foreignObject></svg>", "div")]
    [InlineData("<svg><desc><span>x</span></desc></svg>", "span")]
    [InlineData("<svg><title><b>x</b></title></svg>", "b")]
    [InlineData("<math><annotation-xml encoding='text/html'><div>x</div></annotation-xml></math>", "div")]
    [InlineData("<math><annotation-xml encoding='Application/XHTML+XML'><div>x</div></annotation-xml></math>", "div")]
    // MathML text integration points.
    [InlineData("<math><mi><b>x</b></mi></math>", "b")]
    [InlineData("<math><mtext><span>x</span></mtext></math>", "span")]
    public void Content_At_An_Integration_Point_Is_Html(string html, string localName)
    {
        var element = Element(Parse(html), localName);

        Assert.Equal(DomNamespaces.Html, element.NamespaceUri);
        Assert.Equal("x", element.TextContent);
        Assert.False(HtmlForeignContentTestsHelper.IsHtml(element.ParentNode as DomElement));
    }

    [Fact(Timeout = 600000)]
    public void Mglyph_And_Malignmark_Stay_MathML_At_A_Text_Integration_Point()
    {
        var document = Parse("<math><mi><mglyph></mglyph><malignmark></malignmark></mi></math>");

        Assert.Equal(DomNamespaces.MathMl, Element(document, "mglyph").NamespaceUri);
        Assert.Equal(DomNamespaces.MathMl, Element(document, "malignmark").NamespaceUri);
    }

    [Fact(Timeout = 600000)]
    public void Svg_Inside_Annotation_Xml_Is_Svg()
    {
        var document = Parse("<math><annotation-xml><svg><rect/></svg></annotation-xml></math>");

        Assert.Equal(DomNamespaces.Svg, Element(document, "svg").NamespaceUri);
        Assert.Equal(DomNamespaces.Svg, Element(document, "rect").NamespaceUri);
        Assert.Equal("annotation-xml", Element(document, "svg").ParentNode is DomElement parent ? parent.LocalName : null);
    }

    [Theory(Timeout = 600000)]
    // The HTML start tags that cannot appear in foreign content close it and are inserted after it.
    [InlineData("<svg><circle/><p>x</p></svg>", "<body><svg><circle></circle></svg><p>x</p></body>")]
    [InlineData("<svg><g><div>x</div></g></svg>", "<body><svg><g></g></svg><div>x</div></body>")]
    [InlineData("<svg><img src='a'>", "<body><svg></svg><img src=\"a\"></body>")]
    [InlineData("<math><mrow><table><tr><td>x</td></tr></table>", "<body><math><mrow></mrow></math><table><tbody><tr><td>x</td></tr></tbody></table></body>")]
    // An annotation-xml with no HTML encoding is no integration point.
    [InlineData("<math><annotation-xml><div>x</div></annotation-xml></math>", "<body><math><annotation-xml></annotation-xml></math><div>x</div></body>")]
    // A font breaks out only with color, face or size.
    [InlineData("<svg><font color='red'>x</font></svg>", "<body><svg></svg><font color=\"red\">x</font></body>")]
    [InlineData("<svg><font>x</font></svg>", "<body><svg><font>x</font></svg></body>")]
    // </p> and </br> break out too, then do what they do in HTML.
    [InlineData("<svg><g></p>x", "<body><svg><g></g></svg><p></p>x</body>")]
    [InlineData("<svg></br>x", "<body><svg></svg><br>x</body>")]
    // Inside an integration point the same tags are HTML content and stay where they are.
    [InlineData("<svg><foreignObject><p>x</p></foreignObject></svg>", "<body><svg><foreignObject><p>x</p></foreignObject></svg></body>")]
    public void An_Html_Start_Tag_Ends_Foreign_Content(string html, string body) =>
        Assert.Equal(body, Body(html));

    [Theory(Timeout = 600000)]
    // A self-closing flag is acknowledged in foreign content: nothing is swallowed.
    [InlineData("<svg><path d='M0 0'/><g><rect/></g></svg>x", "<body><svg><path d=\"M0 0\"></path><g><rect></rect></g></svg>x</body>")]
    [InlineData("<math><mi/><mo>+</mo></math>", "<body><math><mi></mi><mo>+</mo></math></body>")]
    [InlineData("<p><svg/>x</p>", "<body><p><svg></svg>x</p></body>")]
    // A void name is an ordinary element in SVG: an SVG link takes children and an end tag.
    [InlineData("<svg><link>x</link></svg>", "<body><svg><link>x</link></svg></body>")]
    public void A_Self_Closing_Foreign_Element_Opens_Nothing(string html, string body) =>
        Assert.Equal(body, Body(html));

    [Theory(Timeout = 600000)]
    // An end tag closes the nearest foreign element of its name, in any case, and what is open inside it.
    [InlineData("<svg><g><rect></svg>x", "<body><svg><g><rect></rect></g></svg>x</body>")]
    [InlineData("<svg><foreignObject><div>x</div></FOREIGNOBJECT>y</svg>", "<body><svg><foreignObject><div>x</div></foreignObject>y</svg></body>")]
    // One that reaches an HTML element first is the HTML rules', which close the div and the svg in it.
    [InlineData("<div><svg><g></div>x", "<body><div><svg><g></g></svg></div>x</body>")]
    // An HTML </title> does not close an SVG title: the div at the integration point keeps it out.
    [InlineData("<svg><title><div>t</title></div></svg>x", "<body><svg><title><div>t</div></title></svg>x</body>")]
    // A stray end tag inside SVG reaches the div, which no end tag with no rule of its own passes.
    [InlineData("<div><svg><g></path></g></svg>x</div>", "<body><div><svg><g></g></svg>x</div></body>")]
    public void An_End_Tag_In_Foreign_Content_Closes_Its_Element(string html, string body) =>
        Assert.Equal(body, Body(html));

    [Fact(Timeout = 600000)]
    public void An_Svg_Style_Is_Markup_Not_Raw_Text()
    {
        var style = Element(Parse("<svg><style>a &amp; b<rect/></style></svg>"), "style");

        Assert.Equal(DomNamespaces.Svg, style.NamespaceUri);
        Assert.Equal("a & b", style.FirstChild!.NodeValue);
        Assert.Equal(DomNamespaces.Svg, Assert.IsType<DomElement>(style.LastChild).NamespaceUri);
    }

    [Theory(Timeout = 600000)]
    [InlineData("style")]
    [InlineData("script")]
    public void Decoded_Svg_Raw_Text_Names_Survive_A_Serialize_And_Reparse_Round_Trip(string name)
    {
        // In foreign content the references are decoded, so this text holds a literal end tag and an
        // img start tag. The serializer escapes an SVG style's or script's text (it is not HTML raw
        // text); writing it raw would close the element on re-parse and insert the img — the mutation
        // XSS that a serializer without the namespace (an adapter with no GetNamespace) is open to.
        var document = Parse($"<svg><{name}>&lt;/{name}&gt;&lt;img id=injected&gt;</{name}></svg>");
        Assert.Equal($"</{name}><img id=injected>", Element(document, name).TextContent);

        var serialized = HtmlSerializer.Serialize(document.Body!);
        Assert.Equal($"<body><svg><{name}>&lt;/{name}&gt;&lt;img id=injected&gt;</{name}></svg></body>", serialized);
        Assert.Null(Parse(serialized).GetElementById("injected"));
    }

    [Fact(Timeout = 600000)]
    public void An_Svg_Title_Is_Markup_Not_Rcdata()
    {
        var document = Parse("<svg><title>Icon <b>bold</b></title></svg>");
        var title = Element(document, "title");

        Assert.Equal(DomNamespaces.Svg, title.NamespaceUri);
        Assert.Equal(DomNamespaces.Html, Element(document, "b").NamespaceUri);
        Assert.Equal("Icon bold", title.TextContent);
    }

    [Fact(Timeout = 600000)]
    public void An_Svg_Script_Has_Its_References_Decoded()
    {
        var script = Element(Parse("<svg><script>if (a &lt; b) f()</script></svg>"), "script");

        Assert.Equal(DomNamespaces.Svg, script.NamespaceUri);
        Assert.Equal("if (a < b) f()", script.TextContent);
    }

    [Theory(Timeout = 600000)]
    // After the SVG, an HTML style is RAWTEXT again.
    [InlineData("<svg><style>x</style></svg><style>a<b>{}</style>", "a<b>{}")]
    // An svg left open is closed by the HTML end tag that reaches past it.
    [InlineData("<div><svg><g></div><style>p<q>{}</style>", "p<q>{}")]
    // At an HTML integration point a style is HTML.
    [InlineData("<svg><foreignObject><style>a<b>{}</style></foreignObject></svg>", "a<b>{}")]
    public void An_Html_Style_Is_Still_Raw_Text(string html, string text)
    {
        var style = Elements(Parse(html)).Last(element => element.LocalName == "style");

        Assert.Equal(DomNamespaces.Html, style.NamespaceUri);
        Assert.Equal(text, Assert.Single(style.ChildNodes).NodeValue);
    }

    [Fact(Timeout = 600000)]
    public void The_Tokenizer_On_Its_Own_Still_Reads_An_Svg_Style_As_Raw_Text()
    {
        // With no tree builder behind it the tokenizer cannot tell foreign content from HTML, and a
        // scan that guessed wrong after an unclosed <svg> would read every later script as markup.
        var tokens = new HtmlTokenizer().Tokenize("<svg><style><rect/></style></svg>")
            .Select(token => $"{token.Type}:{token.Name ?? token.Data}")
            .ToArray();

        Assert.Equal(["StartTag:svg", "StartTag:style", "Character:<rect/>", "EndTag:style", "EndTag:svg", "EndOfFile:"], tokens);
    }

    [Theory(Timeout = 600000)]
    [InlineData("<svg><style><![CDATA[a > b && c]]></style></svg>", "style", "a > b && c")]
    // The text before, inside and after a CDATA section is one text node.
    [InlineData("<svg><text>x<![CDATA[<y>&amp;]]>z</text></svg>", "text", "x<y>&amp;z")]
    [InlineData("<math><ms><![CDATA[a]]]]></ms></math>", "ms", "a]]")]
    [InlineData("<svg><text><![CDATA[abc", "text", "abc")]
    public void A_Cdata_Section_In_Foreign_Content_Is_Text(string html, string localName, string text)
    {
        var element = Element(Parse(html), localName);

        Assert.Equal(text, Assert.Single(element.ChildNodes).NodeValue);
    }

    [Fact(Timeout = 600000)]
    public void A_Cdata_Section_In_Html_Content_Is_Still_A_Comment()
    {
        var div = Element(Parse("<div><![CDATA[x]]></div>"), "div");

        Assert.IsType<DomComment>(Assert.Single(div.ChildNodes));
    }

    [Fact(Timeout = 600000)]
    public void A_Table_Fosters_An_Svg_Out_Like_Other_Content_It_Does_Not_Take()
    {
        // Where in the body it lands is this builder's foster parenting, which appends to the table's
        // parent rather than inserting before the table; that is not what this asserts.
        var document = Parse("<table><svg><rect/></svg><tr><td>x</td></tr></table>");
        var svg = Element(document, "svg");

        Assert.Same(document.Body, svg.ParentNode);
        Assert.Equal(DomNamespaces.Svg, Element(document, "rect").NamespaceUri);
        Assert.Equal("<table><tbody><tr><td>x</td></tr></tbody></table>", HtmlSerializer.Serialize(Element(document, "table")));
    }

    [Fact(Timeout = 600000)]
    public void An_Svg_Element_Named_Like_A_Table_Part_Keeps_Its_Text() =>
        Assert.Equal("<body><svg><tr>x</tr></svg></body>", Body("<svg><tr>x</tr></svg>"));

    [Theory(Timeout = 600000)]
    [InlineData("<svg><p>x</p>", "unexpected-html-element-in-foreign-content")]
    [InlineData("<math><mi>x</mi></p>", "unexpected-html-element-in-foreign-content", "unexpected-end-tag")]
    [InlineData("<svg><g><rect></svg>", "end-tag-closes-open-elements")]
    [InlineData("<svg><text><![CDATA[x", "eof-in-cdata", "unclosed-element", "unclosed-element")]
    public void Foreign_Content_Errors_Are_Reported(string html, params string[] codes)
    {
        var diagnostics = HtmlDocumentParser.ParseDocument(
            "<!DOCTYPE html>" + html, null, new HtmlParseOptions { ReportParseErrors = true }).Diagnostics;

        Assert.Equal(codes, diagnostics.Select(diagnostic => diagnostic.Code).ToArray());
    }

    [Fact(Timeout = 600000)]
    public void A_Fragment_In_An_Svg_Context_Is_Svg()
    {
        var fragment = HtmlDocumentParser.ParseFragment("<rect/><foreignObject><p>x</p></foreignObject>", "svg").Fragment;

        var elements = Elements(fragment).ToArray();
        Assert.Equal(["rect", "foreignObject", "p"], elements.Select(element => element.LocalName).ToArray());
        Assert.Equal(
            [DomNamespaces.Svg, DomNamespaces.Svg, DomNamespaces.Html],
            elements.Select(element => element.NamespaceUri).ToArray());
    }

    [Fact(Timeout = 600000)]
    public void Inline_Svg_And_MathML_Round_Trip_Through_The_Serializer()
    {
        const string body =
            "<body><p>An icon " +
            "<svg xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" viewBox=\"0 0 10 10\" preserveAspectRatio=\"xMidYMid meet\">" +
            "<title>Icon</title>" +
            "<defs><linearGradient id=\"g\" gradientUnits=\"userSpaceOnUse\"><stop offset=\"0\" stop-color=\"red\"></stop></linearGradient>" +
            "<clipPath id=\"c\"><rect width=\"5\" height=\"5\"></rect></clipPath>" +
            "<filter id=\"f\"><feGaussianBlur stdDeviation=\"1\"></feGaussianBlur></filter></defs>" +
            "<style>.a { fill: url(#g) }</style>" +
            "<use xlink:href=\"#r\" xml:lang=\"en\"></use>" +
            "<text><textPath xlink:href=\"#p\">t</textPath></text>" +
            "<foreignObject width=\"10\" height=\"10\"><div>html <b>here</b><br></div></foreignObject>" +
            "</svg> and a formula " +
            "<math><mi definitionURL=\"u\">x</mi><mo>+</mo>" +
            "<annotation-xml encoding=\"text/html\"><p>y</p></annotation-xml></math></p></body>";
        var document = Parse(body);

        Assert.Equal(body, HtmlSerializer.Serialize(document.Body!));
    }
}

/// <summary>A namespace test the assertions above share.</summary>
internal static class HtmlForeignContentTestsHelper
{
    public static bool IsHtml(DomElement? element) =>
        element is not null && element.NamespaceUri == DomNamespaces.Html;
}
