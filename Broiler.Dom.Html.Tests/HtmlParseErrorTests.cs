using Broiler.Dom;
using Broiler.Dom.Html;
using Xunit;

namespace Broiler.Dom.Html.Tests;

/// <summary>
/// <see cref="HtmlParseOptions.ReportParseErrors"/>: a document parse that is asked adds each parse
/// error it meets to its diagnostics, with a code, a line and a column, and builds the same tree.
/// </summary>
/// <remarks>
/// The question these answer is the one a page analysis asks of a page that renders wrong: which
/// markup did the parser have to repair, and how. Most cases are written to produce exactly one
/// error, so a report that fires where it should not fails as surely as one that is missing.
/// </remarks>
public sealed class HtmlParseErrorTests
{
    private static readonly HtmlParseOptions Reporting = new() { ReportParseErrors = true };

    private static IReadOnlyList<HtmlParseDiagnostic> Errors(string html) =>
        HtmlDocumentParser.ParseDocument(html, null, Reporting).Diagnostics;

    private static string[] Codes(string html) => Errors(html).Select(error => error.Code ?? "(no code)").ToArray();

    private static string Token(HtmlToken token) =>
        $"{token.Type}:{token.Name}:{token.Data}:{string.Join(",", token.Attributes.Select(a => $"{a.Key}={a.Value}"))}:{token.SelfClosing}";

    [Theory(Timeout = 600000)]
    [InlineData("<p a=1 a=2></p>", "duplicate-attribute")]
    [InlineData("<div", "eof-in-tag")]
    [InlineData("<a href=\"x", "eof-in-tag")]
    [InlineData("<", "eof-before-tag-name")]
    [InlineData("a < b", "invalid-first-character-of-tag-name")]
    [InlineData("</ x>", "invalid-first-character-of-tag-name")]
    [InlineData("<?xml version=\"1.0\"?>", "unexpected-question-mark-instead-of-tag-name")]
    [InlineData("</>", "missing-end-tag-name")]
    [InlineData("<!-- x", "eof-in-comment")]
    [InlineData("<!-->", "abrupt-closing-of-empty-comment")]
    [InlineData("<!--->", "abrupt-closing-of-empty-comment")]
    [InlineData("<![CDATA[x]]>", "cdata-in-html-content")]
    [InlineData("<!x>", "incorrectly-opened-comment")]
    [InlineData("<script>alert(1)", "eof-in-text")]
    [InlineData("<textarea>x", "eof-in-text")]
    [InlineData("<a href=\"x\"title=\"y\"></a>", "missing-whitespace-between-attributes")]
    [InlineData("<a href=></a>", "missing-attribute-value")]
    [InlineData("<a b\"c></a>", "unexpected-character-in-attribute-name")]
    [InlineData("<a href=x\"y></a>", "unexpected-character-in-unquoted-attribute-value")]
    [InlineData("<a / href=x></a>", "unexpected-solidus-in-tag")]
    [InlineData("<a =x></a>", "unexpected-equals-sign-before-attribute-name")]
    [InlineData("<p></p x=1>", "end-tag-with-attributes")]
    [InlineData("<p></p/>", "end-tag-with-trailing-solidus")]
    public void ATokenizerErrorIsReportedByTheCodeTheStandardGivesIt(string markup, string code) =>
        Assert.Equal([code], Codes("<!DOCTYPE html>" + markup));

    [Theory(Timeout = 600000)]
    [InlineData("<!DOCTYPE>", "missing-doctype-name")]
    [InlineData("<!DOCTYPE html", "eof-in-doctype")]
    [InlineData("<!DOCTYPE", "eof-in-doctype")]
    [InlineData("<p>x", "missing-doctype")]
    [InlineData("<!-- a comment -->\n<p>x", "missing-doctype")]
    [InlineData("<!DOCTYPE html><p>x</p><!DOCTYPE html>", "unexpected-doctype")]
    [InlineData("<!DOCTYPE html PUBLIC \"-//W3C//DTD HTML 4.01//EN\">", "legacy-doctype")]
    [InlineData("<!DOCTYPE svg>", "legacy-doctype")]
    public void ADoctypeErrorIsReported(string markup, string code) =>
        Assert.Equal([code], Codes(markup));

    [Theory(Timeout = 600000)]
    [InlineData("<div/>x", "non-void-html-element-start-tag-with-trailing-solidus")]
    [InlineData("<svg><foreignObject><span/></foreignObject></svg>", "non-void-html-element-start-tag-with-trailing-solidus")]
    [InlineData("x</span>y", "unexpected-end-tag")]
    [InlineData("</br>", "unexpected-end-tag")]
    [InlineData("<img src=\"x\"></img>", "unexpected-end-tag")]
    [InlineData("<div><span>x</div>", "end-tag-closes-open-elements")]
    [InlineData("<main>", "unclosed-element")]
    public void ATreeConstructionErrorIsReported(string markup, string code) =>
        Assert.Equal([code], Codes("<!DOCTYPE html>" + markup));

    // Markup the Standard allows: optional end tags, void and foreign self-closing tags, a DOCTYPE
    // the "initial" insertion mode accepts.
    [Theory(Timeout = 600000)]
    [InlineData("<!DOCTYPE html><html><head><title>t</title></head><body><p>x</p></body></html>")]
    [InlineData("<!DOCTYPE html><ul><li>a<li>b</ul><p>c<dl><dt>d<dd>e</dl>")]
    [InlineData("<!DOCTYPE html><table><tr><td>x<td>y<tr><td>z</table>")]
    [InlineData("<!DOCTYPE html><br/><img src=\"x\"/><input type=checkbox checked>")]
    [InlineData("<!DOCTYPE html><svg><path d=\"M0 0\"/><circle r=\"1\"/></svg><math><mi/></math>")]
    [InlineData("<!DOCTYPE html><p>An icon <svg/> and a formula <math/></p>")]
    [InlineData("<!DOCTYPE html SYSTEM \"about:legacy-compat\"><p>x")]
    [InlineData("<!doctype HTML>\n<p>x</p>")]
    public void ConformingMarkupReportsNothing(string markup) => Assert.Empty(Errors(markup));

    [Fact(Timeout = 600000)]
    public void NothingIsReportedUnlessAskedAndAskingDoesNotChangeTheTree()
    {
        const string markup = "<p a=1 a=2>x</span><div/><main><!-- y";

        Assert.Empty(HtmlDocumentParser.ParseDocument(markup).Diagnostics);
        Assert.Empty(HtmlDocumentParser.ParseDocument(markup, null, new HtmlParseOptions()).Diagnostics);

        var quiet = HtmlDocumentParser.ParseDocument(markup).Document;
        var reported = HtmlDocumentParser.ParseDocument(markup, null, Reporting);
        Assert.NotEmpty(reported.Diagnostics);
        Assert.Equal(HtmlSerializer.Serialize(quiet), HtmlSerializer.Serialize(reported.Document));
    }

    [Fact(Timeout = 600000)]
    public void AFragmentParseReportsNothing() =>
        Assert.Empty(HtmlDocumentParser.ParseFragment("<p a=1 a=2>x</span><div/>", "div", Reporting).Diagnostics);

    // Input stream preprocessing turns each CRLF into one LF, so the offset counts the LF-only input;
    // the line and column are the same in either.
    [Fact(Timeout = 600000)]
    public void AnErrorIsLocatedByTheLineAndColumnOfTheOriginalInput()
    {
        var error = Assert.Single(Errors("<!DOCTYPE html>\r\n<p>x</p>\r\n  <div"));

        Assert.Equal("eof-in-tag", error.Code);
        Assert.Equal(3, error.Line);
        Assert.Equal(3, error.Column);
        Assert.Equal("<!DOCTYPE html>\n<p>x</p>\n  ".Length, error.SourceOffset);
    }

    [Fact(Timeout = 600000)]
    public void AnUnclosedElementIsLocatedAtItsStartTagOutermostFirst()
    {
        var errors = Errors("<!DOCTYPE html>\n<div>\n  <p>x\n  <section>\n");

        Assert.Equal(
            ["unclosed-element@2:1 <div>", "unclosed-element@4:3 <section>"],
            errors.Select(error => $"{error.Code}@{error.Line}:{error.Column} {error.Message[..error.Message.IndexOf('>', StringComparison.Ordinal)]}>"));
    }

    [Theory(Timeout = 600000)]
    [InlineData("<div><span>x</p>", "</p> matches no open <p> and stands for an empty one, as in a browser.")]
    [InlineData("<div>x</span>", "</span> matches no open element and is ignored.")]
    [InlineData("<div><table><tr><td>x</div>", "</div> is ignored: the open <div> is outside the <td> it would have to close first.")]
    [InlineData("<span><div>x</span>", "</span> is ignored: the open <span> is outside the <div> it would have to close first.")]
    [InlineData("<h1>x</h2>", "</h2> closes the open <h1>, as a browser does.")]
    [InlineData("a</br>", "</br> is read as <br>, a line break, as a browser reads it.")]
    public void AnEndTagTheBuilderRepairsSaysWhatItDid(string markup, string message)
    {
        var error = Errors("<!DOCTYPE html>" + markup).First();

        Assert.Equal("unexpected-end-tag", error.Code);
        Assert.Equal(message, error.Message);
    }

    [Fact(Timeout = 600000)]
    public void AnEndTagThatClosesOthersNamesThem()
    {
        var error = Assert.Single(Errors("<!DOCTYPE html><section><div><b><i>x</section>"));

        Assert.Equal("end-tag-closes-open-elements", error.Code);
        Assert.Contains("<i>, <b>, <div>", error.Message, StringComparison.Ordinal);
    }

    [Fact(Timeout = 600000)]
    public void TheTokenizerReportsToACallersListAndEmitsTheSameTokens()
    {
        const string markup = "<p a=1 a=2 b=\"x\"c>y</p x><!-- z";
        var errors = new List<HtmlParseDiagnostic>();

        var reported = new HtmlTokenizer().Tokenize(markup, errors).Select(Token).ToArray();
        var quiet = new HtmlTokenizer().Tokenize(markup).Select(Token).ToArray();

        Assert.Equal(quiet, reported);
        Assert.Equal(
            ["duplicate-attribute", "missing-whitespace-between-attributes", "end-tag-with-attributes", "eof-in-comment"],
            errors.Select(error => error.Code));
    }
}
