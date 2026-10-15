using System.Collections.Generic;
using System.Linq;
using Broiler.Dom;
using Broiler.Dom.Html;
using Xunit;

namespace Broiler.Dom.Html.Tests;

/// <summary>
/// A fragment whose context is an SVG or MathML element is parsed as HTML §13.4 parses it: the
/// context is the adjusted current node until the markup opens an element, so the tree construction
/// dispatcher (§13.2.6) applies the rules for foreign content, or the rules for HTML content at an
/// integration point. Each expected tree is the one a browser builds for <c>innerHTML</c>.
/// </summary>
/// <remarks>
/// <c>ParseFragment</c> used to take a context tag name only and open an HTML element of that name
/// ahead of the markup, so <c>g.innerHTML = '&lt;rect/&gt;'</c> gave an HTML <c>rect</c>; only an
/// <c>svg</c> or <c>math</c> context started foreign content.
/// </remarks>
public sealed class ForeignFragmentParsingTests
{
    private static readonly DomDocument Owner = new();

    private static DomElement Svg(string localName) => Owner.CreateElementNS(DomNamespaces.Svg, localName);

    private static DomElement MathMl(string localName) => Owner.CreateElementNS(DomNamespaces.MathMl, localName);

    private static DomDocumentFragment Parse(string html, DomElement context) =>
        HtmlDocumentParser.ParseFragment(html, context).Fragment;

    private static IEnumerable<DomElement> Elements(DomNode root) => root.Descendants().OfType<DomElement>();

    /// <summary>Each element as namespace-prefix:local-name, in tree order.</summary>
    private static string[] Names(DomNode root) =>
        Elements(root).Select(element => element.NamespaceUri switch
        {
            DomNamespaces.Html => "html:",
            DomNamespaces.Svg => "svg:",
            DomNamespaces.MathMl => "math:",
            _ => "?:",
        } + element.LocalName).ToArray();

    [Fact(Timeout = 600000)]
    public void Markup_In_An_Svg_G_Is_Svg()
    {
        var fragment = Parse("<rect width=1></rect><circle r=1/><g><path/></g>", Svg("g"));

        Assert.Equal(["svg:rect", "svg:circle", "svg:g", "svg:path"], Names(fragment));
    }

    [Fact(Timeout = 600000)]
    public void Markup_In_An_Svg_Context_Takes_The_Svg_Name_Adjustments()
    {
        var fragment = Parse(
            "<lineargradient gradientunits=userSpaceOnUse></lineargradient><use xlink:href=#a></use>",
            Svg("defs"));

        var gradient = Assert.IsType<DomElement>(fragment.FirstChild);
        Assert.Equal("linearGradient", gradient.LocalName);
        Assert.Equal(DomNamespaces.Svg, gradient.NamespaceUri);
        Assert.Equal("gradientUnits", Assert.Single(gradient.Attributes.Values).LocalName);

        var use = Assert.IsType<DomElement>(fragment.LastChild);
        Assert.Equal("#a", use.GetAttributeNS(DomNamespaces.XLink, "href"));
    }

    [Fact(Timeout = 600000)]
    public void The_String_Overload_Still_Takes_An_Html_Context_Of_The_Name()
    {
        // The string overloads keep their behaviour: a name is an HTML element, unless it is svg or math.
        Assert.Equal(["html:rect"], Names(HtmlDocumentParser.ParseFragment("<rect/>", "g").Fragment));
        Assert.Equal(["svg:rect"], Names(HtmlDocumentParser.ParseFragment("<rect/>", "svg").Fragment));
    }

    [Fact(Timeout = 600000)]
    public void An_Svg_Or_Math_Element_Context_Parses_As_Its_Name_Does() =>
        Assert.Equal(
            Names(HtmlDocumentParser.ParseFragment("<rect/><foreignObject><p>x</p></foreignObject>", "svg").Fragment),
            Names(Parse("<rect/><foreignObject><p>x</p></foreignObject>", Svg("svg"))));

    [Fact(Timeout = 600000)]
    public void Markup_In_A_ForeignObject_Is_Html()
    {
        // An HTML integration point: start tags and text are HTML content, so even <rect> is an HTML
        // element there, and an <svg> starts foreign content again.
        var fragment = Parse("<div><b>x</b></div><rect></rect><svg><rect/></svg>", Svg("foreignObject"));

        Assert.Equal(["html:div", "html:b", "html:rect", "svg:svg", "svg:rect"], Names(fragment));
    }

    [Theory(Timeout = 600000)]
    [InlineData("desc")]
    [InlineData("title")]
    public void Markup_In_An_Svg_Desc_Or_Title_Is_Html(string context) =>
        Assert.Equal(["html:b"], Names(Parse("<b>x</b>", Svg(context))));

    [Fact(Timeout = 600000)]
    public void An_Integration_Point_Name_Is_Matched_Exactly()
    {
        // createElementNS(svg, 'foreignobject') is not the SVG foreignObject element, as in a browser.
        Assert.Equal(["svg:figure"], Names(Parse("<figure></figure>", Svg("foreignobject"))));
    }

    [Theory(Timeout = 600000)]
    [InlineData("mi")]
    [InlineData("mo")]
    [InlineData("mn")]
    [InlineData("ms")]
    [InlineData("mtext")]
    public void Markup_In_A_MathML_Text_Integration_Point_Is_Html_Except_Mglyph_And_Malignmark(string context)
    {
        // html5lib-tests foreign-fragment.dat: text and start tags are HTML content, apart from
        // <mglyph> and <malignmark>, which stay MathML.
        var fragment = Parse("<b></b><mglyph/><i></i><malignmark/><u></u>X", MathMl(context));

        Assert.Equal(["html:b", "math:mglyph", "html:i", "math:malignmark", "html:u"], Names(fragment));
        Assert.Equal("X", Assert.IsType<DomText>(fragment.LastChild).Data);
    }

    [Theory(Timeout = 600000)]
    [InlineData("text/html")]
    [InlineData("TEXT/HTML")]
    [InlineData("application/xhtml+xml")]
    public void Markup_In_An_Html_Annotation_Xml_Is_Html(string encoding)
    {
        var context = MathMl("annotation-xml");
        context.SetAttribute("encoding", encoding);

        Assert.Equal(["html:figure", "html:p"], Names(Parse("<figure><p>x</p></figure>", context)));
    }

    [Fact(Timeout = 600000)]
    public void Markup_In_Any_Other_Annotation_Xml_Is_MathML_But_An_Svg_Is_Svg()
    {
        var context = MathMl("annotation-xml");
        context.SetAttribute("encoding", "application/mathml+xml");

        Assert.Equal(["math:figure", "svg:svg", "svg:rect"], Names(Parse("<figure></figure><svg><rect/></svg>", context)));
        Assert.Equal(["math:figure"], Names(Parse("<figure></figure>", MathMl("annotation-xml"))));
    }

    [Fact(Timeout = 600000)]
    public void A_Breakout_Tag_In_A_Foreign_Context_Is_An_Html_Element()
    {
        // The rules for foreign content pop nothing — the context is not on the stack to pop — and
        // reprocess the tag in body, so it is an HTML child of the fragment. Once it closes, the
        // context is the adjusted current node again and the markup after it is SVG.
        var fragment = Parse("<p>x</p><rect/>", Svg("g"));

        Assert.Equal(["html:p", "svg:rect"], Names(fragment));
        Assert.Equal("x", fragment.FirstChild!.TextContent);
    }

    [Theory(Timeout = 600000)]
    [InlineData("<nobr>X", "html:nobr")]
    [InlineData("<font color=red></font>", "html:font")]
    [InlineData("<font></font>", "svg:font")]
    [InlineData("a</p>b", "html:p")]
    [InlineData("a</br>b", "html:br")]
    public void Breakout_Tags_In_An_Svg_Path_Follow_Html5lib(string html, string element) =>
        Assert.Equal([element], Names(Parse(html, Svg("path"))));

    [Fact(Timeout = 600000)]
    public void A_Breakout_Tag_Inside_Foreign_Markup_Closes_It_Back_To_The_Context()
    {
        var fragment = Parse("<g><text><div>x</div></text></g><rect/>", Svg("svg"));

        // The <div> pops the g and the text it is written in, then is a child of the fragment.
        Assert.Equal(["svg:g", "svg:text", "html:div", "svg:rect"], Names(fragment));
        Assert.Equal(["svg:g", "html:div", "svg:rect"], fragment.ChildNodes.OfType<DomElement>().Select(Name).ToArray());
    }

    [Theory(Timeout = 600000)]
    [InlineData("svg", "g")]
    [InlineData("svg", "foreignObject")]
    [InlineData("math", "mi")]
    [InlineData("math", "annotation-xml")]
    public void A_Cdata_Section_In_A_Foreign_Context_Is_Text(string ns, string context)
    {
        // Every SVG and MathML context reads one, integration points included: the adjusted current
        // node is not an HTML element. The text is not decoded, and the text around it joins it.
        var fragment = Parse("a<![CDATA[<b>&amp;]]>c", ns == "svg" ? Svg(context) : MathMl(context));

        Assert.Equal("a<b>&amp;c", Assert.IsType<DomText>(Assert.Single(fragment.ChildNodes)).Data);
    }

    [Fact(Timeout = 600000)]
    public void A_Cdata_Section_In_An_Html_Context_Is_A_Comment() =>
        Assert.IsType<DomComment>(Assert.Single(Parse("<![CDATA[x]]>", Owner.CreateElement("div")).ChildNodes));

    [Theory(Timeout = 600000)]
    [InlineData("style")]
    [InlineData("script")]
    public void An_Svg_Style_Or_Script_Context_Reads_Its_Markup_As_Markup(string context)
    {
        // §13.4 switches the tokenizer for an HTML style or script context only.
        var fragment = Parse("<rect/>&amp;", Svg(context));

        Assert.Equal(["svg:rect"], Names(fragment));
        Assert.Equal("&", Assert.IsType<DomText>(fragment.LastChild).Data);
        Assert.Equal("<rect/>&amp;", Assert.IsType<DomText>(Assert.Single(Parse("<rect/>&amp;", Owner.CreateElement(context)).ChildNodes)).Data);
    }

    [Theory(Timeout = 600000)]
    [InlineData("svg", "path", "</path>X")]
    [InlineData("svg", "g", "<g></path>X")]
    [InlineData("svg", "foreignObject", "</foreignObject>X")]
    [InlineData("svg", "svg", "</svg>X")]
    [InlineData("math", "mi", "</mi>X")]
    [InlineData("math", "math", "</math>X")]
    public void An_End_Tag_Never_Closes_The_Context(string ns, string context, string html)
    {
        var fragment = Parse(html, ns == "svg" ? Svg(context) : MathMl(context));

        Assert.Equal("X", fragment.TextContent);
    }

    [Fact(Timeout = 600000)]
    public void A_Template_Directly_In_A_Foreign_Context_Declares_No_Shadow_Root()
    {
        var fragment = HtmlDocumentParser.ParseFragment(
            "<template shadowrootmode=open><b>x</b></template>",
            Svg("foreignObject"),
            new HtmlParseOptions(AllowDeclarativeShadowRoots: true)).Fragment;

        var template = Assert.IsType<DomElement>(Assert.Single(fragment.ChildNodes));
        Assert.Equal("template", template.LocalName);
        Assert.Equal(["html:b"], Names(template.TemplateContents!));
    }

    [Fact(Timeout = 600000)]
    public void The_Context_Element_Is_Not_Changed()
    {
        var context = MathMl("annotation-xml");
        context.SetAttribute("encoding", "text/html");

        var fragment = Parse("<p>x</p>", context);

        Assert.Empty(context.ChildNodes);
        Assert.Same(Owner, context.OwnerDocument);
        Assert.NotSame(Owner, fragment.OwnerDocument);
    }

    [Fact(Timeout = 600000)]
    public void TryBuildFragment_Parses_An_Svg_Context_By_Its_Namespace()
    {
        // The bridge's innerHTML, outerHTML and insertAdjacentHTML go through this overload. An SVG
        // element named like an HTML void element still hosts the fragment.
        Assert.True(HtmlFragmentParsing.TryBuildFragment(Svg("g"), "<rect/>", out var inG));
        Assert.Equal(["svg:rect"], Names(inG));

        Assert.True(HtmlFragmentParsing.TryBuildFragment(Svg("img"), "<rect/>", out var inImg));
        Assert.Equal(["svg:rect"], Names(inImg));

        Assert.True(HtmlFragmentParsing.TryBuildFragment(Svg("title"), "<b>x</b>", out var inTitle));
        Assert.Equal(["html:b"], Names(inTitle));

        Assert.True(HtmlFragmentParsing.TryBuildFragment(Owner.CreateElement("title"), "<b>x</b>", out var inHtmlTitle));
        Assert.Equal("<b>x</b>", inHtmlTitle.TextContent);
    }

    [Fact(Timeout = 600000)]
    public void A_Foreign_Fragment_Round_Trips_Through_The_Serializer()
    {
        const string markup =
            "<linearGradient id=\"g\" gradientUnits=\"userSpaceOnUse\"></linearGradient>" +
            "<foreignObject width=\"10\"><div>html <b>here</b></div></foreignObject>" +
            "<style>.a { fill: red }</style>";

        Assert.Equal(markup, HtmlSerializer.Serialize(Parse(markup, Svg("g"))));
    }

    private static string Name(DomElement element) =>
        (element.NamespaceUri == DomNamespaces.Html ? "html:" : element.NamespaceUri == DomNamespaces.Svg ? "svg:" : "math:") +
        element.LocalName;
}
