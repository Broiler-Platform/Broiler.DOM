using Broiler.Dom.Html;
using Xunit;

namespace Broiler.Dom.Html.Tests;

public sealed class HtmlSerializerTests
{
    [Theory]
    [InlineData("script")]
    [InlineData("style")]
    [InlineData("noscript")]
    public void Canonical_Raw_Text_Children_Serialize_Literally_By_Default(string tag)
    {
        var document = new DomDocument();
        var element = document.CreateElement(tag);
        element.AppendChild(document.CreateTextNode("a < b"));
        element.AppendChild(document.CreateTextNode(" && c > d"));

        Assert.Equal($"<{tag}>a < b && c > d</{tag}>", HtmlSerializer.Serialize(element));
    }

    [Fact]
    public void Raw_Text_Mode_Does_Not_Leak_To_Siblings_Or_Nested_Elements()
    {
        var document = new DomDocument();
        var fragment = document.CreateDocumentFragment();
        var script = document.CreateElement("script");
        var nested = document.CreateElement("span");
        nested.AppendChild(document.CreateTextNode("a < b"));
        script.AppendChild(nested);
        fragment.AppendChild(script);
        fragment.AppendChild(document.CreateTextNode("a < b"));

        Assert.Equal("<script><span>a &lt; b</span></script>a &lt; b", HtmlSerializer.Serialize(fragment));
    }

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

    [Fact(Timeout = 600000)]
    public void RawText_Element_Text_Serializes_Literally_While_Others_Escape()
    {
        var document = new DomDocument();

        // Raw-text element (<style>): '<' stays literal.
        var style = document.CreateElement("style");
        style.AppendChild(document.CreateTextNode("a < b {}"));
        Assert.Contains("a < b {}", HtmlSerializer.Serialize(style));

        // Non-raw-text element (<div>): '<' is escaped.
        var div = document.CreateElement("div");
        div.AppendChild(document.CreateTextNode("a < b"));
        Assert.Contains("a &lt; b", HtmlSerializer.Serialize(div));
    }
}
