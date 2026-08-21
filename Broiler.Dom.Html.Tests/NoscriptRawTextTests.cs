using System.Linq;
using Broiler.Dom.Html;
using Broiler.Dom;
using Xunit;

namespace Broiler.Dom.Html.Tests;

/// <summary>
/// A scripting-enabled parser takes a <c>&lt;noscript&gt;</c> body as raw text.
/// </summary>
/// <remarks>
/// <para>
/// This is what makes the fallback inert. Parsed as markup — which is what happened before — the
/// contents are live: a nested <c>&lt;script&gt;</c> is a script the page will run, an
/// <c>&lt;img&gt;</c> is a request, and <c>querySelector</c> walks into a subtree no browser would
/// expose. The condition is only that scripting is on; with it off the fallback is meant to render,
/// and is parsed as markup so it can.
/// </para>
/// <para>
/// Raw text also means character references are NOT decoded, the same as
/// <c>&lt;script&gt;</c>/<c>&lt;style&gt;</c> content.
/// </para>
/// </remarks>
public sealed class NoscriptRawTextTests
{
    [Fact(Timeout = 600000)]
    public void TheBodyIsOneRawTextTokenRatherThanTags()
    {
        var tokens = new HtmlTokenizer()
            .Tokenize("<noscript><h2>enable js</h2></noscript>")
            .ToArray();

        Assert.Equal(TokenType.StartTag, tokens[0].Type);
        Assert.Equal("noscript", tokens[0].Name);

        // The markup arrives verbatim as character data — no StartTag token for the <h2>.
        Assert.Equal(TokenType.Character, tokens[1].Type);
        Assert.Equal("<h2>enable js</h2>", tokens[1].Data);

        Assert.Equal(TokenType.EndTag, tokens[2].Type);
        Assert.Equal("noscript", tokens[2].Name);
        Assert.DoesNotContain(tokens, t => t.Type == TokenType.StartTag && t.Name == "h2");
    }

    // The point of the change: nothing inside is live. A script here must never become a script
    // element the host can find and execute.
    [Fact(Timeout = 600000)]
    public void AScriptInsideIsTextAndNotAScriptElement()
    {
        var document = HtmlDocumentParser
            .ParseDocument("<html><body><noscript><script>boom()</script></noscript></body></html>")
            .Document;

        Assert.Empty(document.DocumentElement.Descendants()
            .OfType<DomElement>()
            .Where(e => e.LocalName.Equals("script", System.StringComparison.OrdinalIgnoreCase)));
    }

    [Fact(Timeout = 600000)]
    public void TheContentBecomesASingleTextChild()
    {
        var document = HtmlDocumentParser
            .ParseDocument("<html><body><noscript><p>a</p><p>b</p></noscript></body></html>")
            .Document;

        var noscript = document.DocumentElement.Descendants()
            .OfType<DomElement>()
            .Single(e => e.LocalName.Equals("noscript", System.StringComparison.OrdinalIgnoreCase));

        Assert.Empty(noscript.ChildNodes.OfType<DomElement>());
        Assert.Equal("<p>a</p><p>b</p>", string.Concat(noscript.ChildNodes.Select(n => n.TextContent)));
    }

    // Raw text is not entity-decoded, matching <script>/<style>.
    [Fact(Timeout = 600000)]
    public void CharacterReferencesAreLeftUndecoded()
    {
        var tokens = new HtmlTokenizer()
            .Tokenize("<noscript>a &amp; b</noscript>")
            .ToArray();

        Assert.Equal("a &amp; b", tokens[1].Data);
    }

    // Content around it keeps parsing normally once the end tag closes the raw-text run.
    [Fact(Timeout = 600000)]
    public void ParsingResumesNormallyAfterTheEndTag()
    {
        var document = HtmlDocumentParser
            .ParseDocument("<html><body><noscript><p>hidden</p></noscript><div id='after'>x</div></body></html>")
            .Document;

        Assert.Contains(
            document.DocumentElement.Descendants().OfType<DomElement>(),
            e => e.LocalName.Equals("div", System.StringComparison.OrdinalIgnoreCase));
    }
}
