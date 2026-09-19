using System.Linq;
using Xunit;

namespace Broiler.Dom.Html.Tests;

public sealed class HtmlFragmentParsingTests
{
    [Theory(Timeout = 600000)]
    [InlineData("div", "div")]
    [InlineData("DIV", "div")]
    [InlineData("  SPAN  ", "span")]
    [InlineData("#shadow-root", "div")]
    [InlineData("#document-fragment", "div")]
    [InlineData(null, "div")]
    [InlineData("", "div")]
    public void NormalizeContextTag_Handles_Normal_And_Pseudo_Tags(string? input, string expected)
    {
        Assert.Equal(expected, HtmlFragmentParsing.NormalizeContextTag(input));
    }

    [Theory(Timeout = 600000)]
    [InlineData("div", true)]
    [InlineData("p", true)]
    [InlineData("span", true)]
    [InlineData("table", true)]
    [InlineData("#shadow-root", true)]
    [InlineData("img", false)]
    [InlineData("IMG", false)]
    [InlineData("input", false)]
    [InlineData("br", false)]
    [InlineData("hr", false)]
    [InlineData("area", false)]
    [InlineData("base", false)]
    [InlineData("col", false)]
    [InlineData("embed", false)]
    [InlineData("link", false)]
    [InlineData("meta", false)]
    [InlineData("param", false)]
    [InlineData("source", false)]
    [InlineData("track", false)]
    [InlineData("wbr", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    public void CanHostFragment_Checks_Void_Elements(string? contextTag, bool expected)
    {
        Assert.Equal(expected, HtmlFragmentParsing.CanHostFragment(contextTag));
    }

    [Fact(Timeout = 600000)]
    public void TryBuildFragment_Builds_Fragment_For_Valid_Context()
    {
        var doc = new DomDocument();
        var div = doc.CreateElement("div");

        var success = HtmlFragmentParsing.TryBuildFragment(div, "<p>Hello <b>world</b></p>", out var fragment);
        Assert.True(success);
        Assert.NotNull(fragment);
        Assert.Single(fragment.ChildNodes);
        var p = Assert.IsType<DomElement>(fragment.FirstChild);
        Assert.Equal("p", p.TagName);
        Assert.Equal(2, p.ChildNodes.Count);
    }

    [Fact(Timeout = 600000)]
    public void TryBuildFragment_Fails_For_Void_Context()
    {
        var doc = new DomDocument();
        var img = doc.CreateElement("img");

        var success = HtmlFragmentParsing.TryBuildFragment(img, "<span>text</span>", out var fragment);
        Assert.False(success);
        Assert.Null(fragment);
    }

    [Fact(Timeout = 600000)]
    public void TryBuildFragment_Works_With_Pseudo_Tag_String()
    {
        var success = HtmlFragmentParsing.TryBuildFragment("#shadow-root", "<span>in shadow</span>", out var fragment);
        Assert.True(success);
        Assert.NotNull(fragment);
        var span = Assert.IsType<DomElement>(fragment.FirstChild);
        Assert.Equal("span", span.TagName);
    }
}
