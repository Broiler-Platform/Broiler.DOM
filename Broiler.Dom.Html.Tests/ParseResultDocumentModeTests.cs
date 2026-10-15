using Broiler.Dom.Html;
using Xunit;

namespace Broiler.Dom.Html.Tests;

/// <summary>
/// <see cref="HtmlDocumentParseResult.IsQuirksMode"/> is the tree builder's own document-mode decision
/// (HTML §13.2.6.4.1), and it agrees with <see cref="HtmlDocumentQueries.IsQuirksMode(string?)"/>,
/// which a consumer used to run as a second tokenization of the same source.
/// </summary>
public sealed class ParseResultDocumentModeTests
{
    [Theory(Timeout = 600000)]
    [InlineData("<!DOCTYPE html><p>x", false)]
    [InlineData("<!-- c -->\n  <!DOCTYPE html><p>x", false)]
    [InlineData("<!doctype HTML>", false)]
    [InlineData("<p>x</p><!DOCTYPE html>", true)]
    [InlineData("<p>x", true)]
    [InlineData("", true)]
    [InlineData("   \n", true)]
    [InlineData("<!-- only a comment -->", true)]
    [InlineData("<!DOCTYPE html PUBLIC \"-//W3C//DTD HTML 4.01 Transitional//EN\"><p>x", true)]
    [InlineData("<!DOCTYPE html PUBLIC \"-//W3C//DTD HTML 4.01 Transitional//EN\" \"http://www.w3.org/TR/html4/loose.dtd\"><p>x", false)]
    [InlineData("<!DOCTYPE html PUBLIC \"-//W3C//DTD XHTML 1.0 Strict//EN\" \"http://www.w3.org/TR/xhtml1/DTD/xhtml1-strict.dtd\">", false)]
    [InlineData("<!DOCTYPE svg><p>x", true)]
    [InlineData(" <!DOCTYPE html>", true)]
    public void The_Parse_Reports_The_Mode_The_Token_Scan_Does(string source, bool quirks)
    {
        var result = HtmlDocumentParser.ParseDocument(source);

        Assert.Equal(quirks, result.IsQuirksMode);
        Assert.Equal(HtmlDocumentQueries.IsQuirksMode(source), result.IsQuirksMode);
    }

    [Fact(Timeout = 600000)]
    public void A_Result_Built_Through_Its_Constructor_Is_Not_Quirks() =>
        Assert.False(new HtmlDocumentParseResult(new DomDocument(), string.Empty, []).IsQuirksMode);
}
