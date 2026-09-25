using Broiler.Dom;
using Broiler.Dom.Html;
using Xunit;

namespace Broiler.Dom.Html.Tests;

/// <summary>
/// The document's title is the first HTML title element's text (HTML §4.2.2, the title element;
/// <c>document.title</c> reads it), and an SVG or MathML <c>&lt;title&gt;</c> is an ordinary foreign
/// element where it stands (§13.2.6.5).
/// </summary>
/// <remarks>
/// The tree builder took every <c>&lt;title&gt;</c> outside a template for the document's: it moved
/// an inline icon's SVG title, its tooltip and accessible name, out of the <c>&lt;svg&gt;</c> into
/// the head, and appended the text of every title to the result's <c>Title</c> — so a page titled
/// "Page" with an icon titled "Icon" was "PageIcon", where a browser shows "Page".
/// </remarks>
public sealed class DocumentTitleTests
{
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
}
