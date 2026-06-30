using Broiler.Dom.Html;
using Broiler.Dom;
using Xunit;

namespace Broiler.Dom.Html.Tests;

public sealed class HtmlDocumentParserTests
{
    [Fact]
    public void Tokenizer_Provides_Stable_RawText_And_Attribute_Contract()
    {
        var tokens = new HtmlTokenizer()
            .Tokenize("<script data-x='1'>if (a < b) c();</script>")
            .ToArray();

        Assert.Equal(TokenType.StartTag, tokens[0].Type);
        Assert.Equal("script", tokens[0].Name);
        Assert.Equal("1", tokens[0].Attributes["data-x"]);
        Assert.Equal("if (a < b) c();", tokens[1].Data);
        Assert.Equal(TokenType.EndTag, tokens[2].Type);
    }

    [Fact]
    public void Document_Parser_Creates_Implicit_Structure_And_Table_Section()
    {
        var result = new HtmlDocumentParser().ParseDocument(
            "<title>Shared</title><table><tr><td>cell</td></tr></table>");

        Assert.Equal("Shared", result.Title);
        Assert.NotNull(result.Document.Head);
        Assert.NotNull(result.Document.Body);
        Assert.Single(result.Document.GetElementsByTagName("tbody"));
        Assert.Equal("cell", Assert.IsType<DomText>(
            result.Document.GetElementsByTagName("td").Single().FirstChild).Data);
    }

    [Fact]
    public void Leading_Text_Without_Body_Tag_Opens_The_Body()
    {
        // A document without an explicit <body> that begins with non-whitespace
        // text (ubiquitous in WPT reftests: "Test passes if …") must place that
        // text in the body, not the head — otherwise it never renders and the
        // following content shifts up by a line.
        var result = new HtmlDocumentParser().ParseDocument(
            "<style>.x{}</style>\nTest passes if no red is visible.\n<div></div>");

        var bodyText = result.Document.Body!.ChildNodes
            .OfType<DomText>()
            .Select(t => t.Data)
            .FirstOrDefault(d => d.Contains("Test passes"));
        Assert.NotNull(bodyText);

        // The head holds only the metadata, never the rendered instruction text.
        var headText = string.Concat(result.Document.Head!.ChildNodes
            .OfType<DomText>()
            .Select(t => t.Data));
        Assert.DoesNotContain("Test passes", headText);
    }

    [Fact]
    public void Fragment_Parser_Uses_Context_Sensitive_Table_Rules()
    {
        var result = new HtmlDocumentParser().ParseFragment(
            "<td id='cell'>value</td>",
            "tr");

        var cell = Assert.IsType<DomElement>(Assert.Single(result.Fragment.ChildNodes));
        Assert.Equal("td", cell.LocalName);
        Assert.Equal("cell", cell.Id);
    }

    [Fact]
    public void Serialization_RoundTrip_Is_Deterministic()
    {
        var parser = new HtmlDocumentParser();
        var firstDocument = parser.ParseDocument(
            "<main id='host'><span class='value'>hello</span><!--note--></main>").Document;
        var first = HtmlSerializer.Serialize(
            firstDocument.DocumentElement!,
            new HtmlSerializationOptions(IncludeHtmlDoctype: true));
        var secondDocument = parser.ParseDocument(first).Document;
        var second = HtmlSerializer.Serialize(
            secondDocument.DocumentElement!,
            new HtmlSerializationOptions(IncludeHtmlDoctype: true));

        Assert.Equal(first, second);
    }
}
