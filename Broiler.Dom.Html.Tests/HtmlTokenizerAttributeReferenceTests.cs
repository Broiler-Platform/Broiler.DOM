using Broiler.Dom.Html;
using Xunit;

namespace Broiler.Dom.Html.Tests;

/// <summary>
/// Character references in attribute values (WHATWG §13.2.5, the attribute-value
/// character-reference states). The tokenizer used to leave an attribute value as the source text
/// that spelled it and let the renderer decode it when building boxes — which made the rendering
/// right and everything reading the DOM wrong: <c>getAttribute("href")</c> on
/// <c>href="?a=1&amp;amp;b=2"</c> returned the escaped spelling, an <c>[attr="…"]</c> selector had
/// to be written against it, and serializing re-escaped the ampersand so a round-trip corrupted
/// the value a little more each time.
/// </summary>
public sealed class HtmlTokenizerAttributeReferenceTests
{
    private static string Attribute(string html, string name = "x") =>
        new HtmlTokenizer().Tokenize(html)
            .First(t => t.Type == TokenType.StartTag)
            .Attributes[name];

    [Fact]
    public void Named_References_Are_Decoded()
    {
        Assert.Equal("a\"b", Attribute("""<i x="a&quot;b">"""));
        Assert.Equal("a&b", Attribute("""<i x="a&amp;b">"""));
        Assert.Equal("a<b>c", Attribute("""<i x="a&lt;b&gt;c">"""));
    }

    [Fact]
    public void Numeric_And_Hexadecimal_References_Are_Decoded()
    {
        Assert.Equal("AB", Attribute("""<i x="&#65;&#x42;">"""));
    }

    [Fact]
    public void A_NonBreaking_Space_Is_Decoded_To_Its_Character()
    {
        Assert.Equal("a b", Attribute("""<i x="a&nbsp;b">"""));
    }

    /// <summary>The shape the bug was found through: a query string's escaped separator is an
    /// ampersand, so the URL the DOM reports is the URL the page meant.</summary>
    [Fact]
    public void An_Escaped_Query_Separator_Decodes_To_An_Ampersand()
    {
        Assert.Equal("?a=1&b=2", Attribute("""<a x="?a=1&amp;b=2">"""));
    }

    /// <summary>
    /// One level of escaping, not two: <c>&amp;amp;amp;</c> is the spelling of a literal
    /// <c>&amp;amp;</c>. This is what the removed downstream decode used to eat.
    /// </summary>
    [Fact]
    public void Only_One_Level_Of_Escaping_Is_Removed()
    {
        Assert.Equal("&amp;", Attribute("""<i x="&amp;amp;">"""));
    }

    /// <summary>
    /// A reference with no terminating <c>;</c> is left alone. That is what keeps a query string's
    /// <c>&amp;copy=2</c> from becoming a <c>©</c> — the spec's ambiguous-ampersand rule — at the
    /// cost of its other half, a terminator-less <c>&amp;copy</c> that a browser would resolve.
    /// </summary>
    [Fact]
    public void An_Unterminated_Reference_Is_Left_Literal()
    {
        Assert.Equal("?a=1&copy=2", Attribute("""<a x="?a=1&copy=2">"""));
    }

    /// <summary>Quoting does not change what a value means: an unquoted attribute decodes too.
    /// </summary>
    [Fact]
    public void An_Unquoted_Value_Is_Decoded()
    {
        Assert.Equal("a&b", Attribute("<i x=a&amp;b>"));
    }

    /// <summary>A value with no reference in it is returned unchanged.</summary>
    [Fact]
    public void A_Value_With_No_Reference_Is_Untouched()
    {
        Assert.Equal("plain value", Attribute("""<i x="plain value">"""));
    }
}
