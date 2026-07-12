using Broiler.Dom.Html;
using Xunit;

namespace Broiler.Dom.Html.Tests;

public sealed class HtmlSerializerTests
{
    [Theory]
    [InlineData("script", true)]
    [InlineData("style", true)]
    [InlineData("xmp", true)]
    [InlineData("iframe", true)]
    [InlineData("noembed", true)]
    [InlineData("noframes", true)]
    [InlineData("noscript", true)]
    [InlineData("plaintext", true)]
    [InlineData("SCRIPT", true)]   // case-insensitive
    [InlineData("div", false)]
    [InlineData("p", false)]
    [InlineData("textarea", false)] // escapable-raw-text, but serialized escaped
    public void IsRawTextElement_Matches_The_Standard_RawText_Set(string tagName, bool expected) =>
        Assert.Equal(expected, HtmlSerializer.IsRawTextElement(tagName));

    [Fact]
    public void RawText_Element_Text_Serializes_Literally_While_Others_Escape()
    {
        var document = new DomDocument();

        // Raw-text element (<style>): '<' stays literal.
        var style = document.CreateElement("style");
        style.AppendChild(document.CreateTextNode("a < b {}"));
        Assert.Contains("a < b {}", HtmlSerializer.Serialize(style, new HtmlSerializationOptions(EncodeTextNodes: false)));

        // Non-raw-text element (<div>): '<' is escaped.
        var div = document.CreateElement("div");
        div.AppendChild(document.CreateTextNode("a < b"));
        Assert.Contains("a &lt; b", HtmlSerializer.Serialize(div));
    }
}
