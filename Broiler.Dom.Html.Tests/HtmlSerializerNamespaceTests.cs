using System;
using System.Collections.Generic;
using System.Linq;
using Broiler.Dom;
using Broiler.Dom.Html;
using Xunit;

namespace Broiler.Dom.Html.Tests;

/// <summary>
/// HTML fragment serialization (§13.3) reads an element's namespace before its name. It writes the
/// local name of an HTML, SVG or MathML element and the qualified name of any other, and it applies
/// the void and raw-text rules to HTML elements only.
/// </summary>
/// <remarks>
/// The serializer used to lowercase every name and match void and raw-text names regardless of
/// namespace. Inline SVG came out as <c>&lt;foreignobject&gt;</c> and <c>&lt;lineargradient&gt;</c>,
/// an SVG <c>&lt;link&gt;</c> lost its children and end tag, and the text of an SVG
/// <c>&lt;style&gt;</c> was written unescaped. In foreign content a spec parser reads that text as
/// markup again, so <c>&lt;img src=x onerror=f()&gt;</c> breaks out of the SVG as an HTML
/// <c>img</c>.
/// </remarks>
public sealed class HtmlSerializerNamespaceTests
{
    private const string Payload = "<img src=x onerror=f()>";
    private const string EscapedPayload = "&lt;img src=x onerror=f()&gt;";

    [Fact(Timeout = 600000)]
    public void Svg_Camel_Case_Names_Keep_Their_Case()
    {
        var document = new DomDocument();
        var svg = document.CreateElementNS(DomNamespaces.Svg, "svg");
        var defs = svg.AppendChild(document.CreateElementNS(DomNamespaces.Svg, "defs"));
        defs.AppendChild(document.CreateElementNS(DomNamespaces.Svg, "linearGradient"));
        var foreignObject = svg.AppendChild(document.CreateElementNS(DomNamespaces.Svg, "foreignObject"));
        foreignObject.AppendChild(document.CreateElement("div"));

        Assert.Equal(
            "<svg><defs><linearGradient></linearGradient></defs><foreignObject><div></div></foreignObject></svg>",
            HtmlSerializer.Serialize(svg));
    }

    [Fact(Timeout = 600000)]
    public void Svg_Style_Text_Is_Escaped()
    {
        var document = new DomDocument();
        var svg = document.CreateElementNS(DomNamespaces.Svg, "svg");
        var style = svg.AppendChild(document.CreateElementNS(DomNamespaces.Svg, "style"));
        style.AppendChild(document.CreateTextNode(Payload));

        Assert.Equal($"<svg><style>{EscapedPayload}</style></svg>", HtmlSerializer.Serialize(svg));
    }

    [Theory(Timeout = 600000)]
    [InlineData("script")]
    [InlineData("style")]
    public void MathMl_Raw_Text_Names_Are_Escaped(string name)
    {
        var document = new DomDocument();
        var element = document.CreateElementNS(DomNamespaces.MathMl, name);
        element.AppendChild(document.CreateTextNode(Payload));

        Assert.Equal($"<{name}>{EscapedPayload}</{name}>", HtmlSerializer.Serialize(element));
    }

    [Theory(Timeout = 600000)]
    [InlineData("link")]
    [InlineData("img")]
    [InlineData("br")]
    [InlineData("source")]
    public void Foreign_Elements_With_Void_Names_Keep_Children_And_End_Tag(string name)
    {
        var document = new DomDocument();
        var svg = document.CreateElementNS(DomNamespaces.Svg, "svg");
        var element = svg.AppendChild(document.CreateElementNS(DomNamespaces.Svg, name));
        element.AppendChild(document.CreateTextNode("kept"));

        Assert.Equal($"<svg><{name}>kept</{name}></svg>", HtmlSerializer.Serialize(svg));
    }

    [Fact(Timeout = 600000)]
    public void A_Prefixed_Svg_Element_Is_Written_With_Its_Local_Name()
    {
        var document = new DomDocument();
        var rect = document.CreateElementNS(DomNamespaces.Svg, "svg:rect");

        Assert.Equal("<rect></rect>", HtmlSerializer.Serialize(rect));
    }

    [Fact(Timeout = 600000)]
    public void An_Element_In_Another_Namespace_Is_Written_With_Its_Qualified_Name()
    {
        var document = new DomDocument();
        var element = document.CreateElementNS("urn:example", "x:Widget");
        var style = element.AppendChild(document.CreateElementNS("urn:example", "style"));
        style.AppendChild(document.CreateTextNode(Payload));
        element.AppendChild(document.CreateElementNS(null, "br"));

        Assert.Equal(
            $"<x:Widget><style>{EscapedPayload}</style><br></br></x:Widget>",
            HtmlSerializer.Serialize(element));
    }

    [Fact(Timeout = 600000)]
    public void Html_Elements_Inside_Svg_Keep_The_Html_Rules()
    {
        var document = new DomDocument();
        var svg = document.CreateElementNS(DomNamespaces.Svg, "svg");
        var foreignObject = svg.AppendChild(document.CreateElementNS(DomNamespaces.Svg, "foreignObject"));
        var style = foreignObject.AppendChild(document.CreateElement("style"));
        style.AppendChild(document.CreateTextNode("a < b {}"));
        var br = foreignObject.AppendChild(document.CreateElement("br"));
        br.AppendChild(document.CreateTextNode("lost"));

        Assert.Equal(
            "<svg><foreignObject><style>a < b {}</style><br></foreignObject></svg>",
            HtmlSerializer.Serialize(svg));
    }

    [Fact(Timeout = 600000)]
    public void Html_Namespace_Names_Are_Still_Lowercased()
    {
        var document = new DomDocument();
        var div = document.CreateElementNS(DomNamespaces.Html, "DIV");
        div.AppendChild(document.CreateElementNS(DomNamespaces.Html, "BR"));
        var script = div.AppendChild(document.CreateElementNS(DomNamespaces.Html, "SCRIPT"));
        script.AppendChild(document.CreateTextNode("a < b"));

        Assert.Equal("<div><br><script>a < b</script></div>", HtmlSerializer.Serialize(div));
    }

    // Parsed HTML comes back as it went in. So does parsed SVG, now that the tree builder creates it
    // in the SVG namespace with its camel case: this used to read <foreignobject> on both sides.
    [Fact(Timeout = 600000)]
    public void Parsed_Html_Serializes_As_Before()
    {
        const string body =
            "<body><p>a<br>b<img src=\"x\"></p><style>a < b {}</style><script>if (a < b) {}</script>" +
            "<textarea>&lt;/textarea&gt;</textarea><svg><foreignObject><div>x</div></foreignObject></svg></body>";
        var document = HtmlDocumentParser.ParseDocument("<!DOCTYPE html>" + body).Document;

        Assert.Equal(body, HtmlSerializer.Serialize(document.Body!));
    }

    [Theory(Timeout = 600000)]
    [InlineData("style", DomNamespaces.Html, true)]
    [InlineData("script", DomNamespaces.Html, true)]
    [InlineData("textarea", DomNamespaces.Html, false)]
    [InlineData("style", DomNamespaces.Svg, false)]
    [InlineData("script", DomNamespaces.Svg, false)]
    [InlineData("style", DomNamespaces.MathMl, false)]
    [InlineData("style", null, false)]
    public void IsRawTextElement_Reads_The_Namespace(string localName, string? namespaceUri, bool expected) =>
        Assert.Equal(expected, HtmlSerializer.IsRawTextElement(localName, namespaceUri));

    [Fact(Timeout = 600000)]
    public void An_Adapter_Without_A_Namespace_Is_Serialized_As_Html()
    {
        var root = Element("SVG", null,
            Element("foreignObject", null),
            Element("LINK", null, Text("lost")),
            Element("Style", null, Text("a < b")));

        Assert.Equal(
            "<svg><foreignobject></foreignobject><link><style>a < b</style></svg>",
            HtmlSerializer.Serialize(root, TestNode.Adapter));
    }

    [Fact(Timeout = 600000)]
    public void An_Adapter_With_A_Namespace_Gets_The_Foreign_Rules()
    {
        var root = Element("svg", DomNamespaces.Svg,
            Element("foreignObject", DomNamespaces.Svg, Element("BR", DomNamespaces.Html)),
            Element("link", DomNamespaces.Svg, Text("kept")),
            Element("style", DomNamespaces.Svg, Text(Payload)));
        var adapter = TestNode.Adapter with { GetNamespace = static node => node.Namespace };

        Assert.Equal(
            $"<svg><foreignObject><br></foreignObject><link>kept</link><style>{EscapedPayload}</style></svg>",
            HtmlSerializer.Serialize(root, adapter));
    }

    [Fact(Timeout = 600000)]
    public void Leaf_Text_Reads_The_Namespace_Too()
    {
        // An adapter may hand an element's text back through GetText instead of as child nodes.
        var html = TestNode.Leaf("style", DomNamespaces.Html, "a < b");
        var svg = TestNode.Leaf("style", DomNamespaces.Svg, Payload);
        var adapter = TestNode.Adapter with { GetNamespace = static node => node.Namespace };

        Assert.Equal("<style>a < b</style>", HtmlSerializer.Serialize(html, adapter));
        Assert.Equal($"<style>{EscapedPayload}</style>", HtmlSerializer.Serialize(svg, adapter));
    }

    [Fact(Timeout = 600000)]
    public void The_Seven_Delegate_Constructor_Leaves_The_Namespace_Unset()
    {
        var adapter = new HtmlSerializationAdapter<TestNode>(
            static node => node.Kind,
            static node => node.Name,
            static node => node.Children,
            static _ => [],
            static _ => [],
            static node => node.Text,
            static _ => null);

        Assert.Null(adapter.GetNamespace);
    }

    private static TestNode Element(string name, string? namespaceUri, params TestNode[] children) =>
        new(HtmlSerializationNodeKind.Element, name, namespaceUri, children, null);

    private static TestNode Text(string data) =>
        new(HtmlSerializationNodeKind.Text, string.Empty, null, [], data);

    private sealed record TestNode(
        HtmlSerializationNodeKind Kind,
        string Name,
        string? Namespace,
        IReadOnlyList<TestNode> Children,
        string? Text)
    {
        public static readonly HtmlSerializationAdapter<TestNode> Adapter = new(
            GetKind: static node => node.Kind,
            GetName: static node => node.Name,
            GetChildren: static node => node.Children,
            GetAttributes: static _ => Enumerable.Empty<KeyValuePair<string, string>>(),
            GetStyles: static _ => Enumerable.Empty<KeyValuePair<string, string>>(),
            GetText: static node => node.Text,
            GetRawInnerHtml: static _ => null);

        public static TestNode Leaf(string name, string? namespaceUri, string text) =>
            new(HtmlSerializationNodeKind.Element, name, namespaceUri, [], text);
    }
}
