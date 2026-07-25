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
    public void Tokenizer_Parses_Doctype_Name_And_Public_System_Identifiers()
    {
        var publicToken = new HtmlTokenizer()
            .Tokenize("<!DOCTYPE html PUBLIC \"-//W3C//DTD XHTML 1.0 Strict//EN\" " +
                      "\"http://www.w3.org/TR/xhtml1/DTD/xhtml1-strict.dtd\">")
            .First(t => t.Type == TokenType.Doctype);
        Assert.Equal("html", publicToken.Name);
        Assert.Equal("-//W3C//DTD XHTML 1.0 Strict//EN", publicToken.PublicId);
        Assert.Equal("http://www.w3.org/TR/xhtml1/DTD/xhtml1-strict.dtd", publicToken.SystemId);

        var systemToken = new HtmlTokenizer()
            .Tokenize("<!DOCTYPE root SYSTEM \"about:legacy-compat\">")
            .First(t => t.Type == TokenType.Doctype);
        Assert.Equal("root", systemToken.Name);
        Assert.Equal("", systemToken.PublicId);
        Assert.Equal("about:legacy-compat", systemToken.SystemId);

        // A bare doctype carries no identifiers, and the name is lowercased.
        var bare = new HtmlTokenizer().Tokenize("<!DOCTYPE HTML>")
            .First(t => t.Type == TokenType.Doctype);
        Assert.Equal("html", bare.Name);
        Assert.Equal("", bare.PublicId);
        Assert.Equal("", bare.SystemId);
    }

    [Fact]
    public void Document_Parser_Carries_Doctype_Identifiers_Onto_The_DocumentType_Node()
    {
        var result = new HtmlDocumentParser().ParseDocument(
            "<!DOCTYPE html PUBLIC \"-//W3C//DTD HTML 4.01//EN\" " +
            "\"http://www.w3.org/TR/html4/strict.dtd\"><html><body>x</body></html>");

        var doctype = result.Document.DocumentType;
        Assert.NotNull(doctype);
        Assert.Equal("html", doctype!.Name);
        Assert.Equal("-//W3C//DTD HTML 4.01//EN", doctype.PublicId);
        Assert.Equal("http://www.w3.org/TR/html4/strict.dtd", doctype.SystemId);
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
