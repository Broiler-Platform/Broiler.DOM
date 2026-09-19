using System.Linq;
using Xunit;

namespace Broiler.Dom.Html.Tests;

public sealed class HtmlMetaScannerTests
{
    [Fact(Timeout = 600000)]
    public void FindHttpEquivContent_And_CspPolicy_Find_Matching_Content()
    {
        var html = """
            <html>
            <head>
                <!-- <meta http-equiv="Content-Security-Policy" content="script-src 'none'"> -->
                <meta http-equiv="content-security-policy" content="default-src 'self'; img-src *">
                <meta http-equiv="refresh" content="5; url=https://example.com">
            </head>
            </html>
            """;

        Assert.Equal("default-src 'self'; img-src *", HtmlMetaScanner.FindCspPolicyContent(html));
        Assert.Equal("default-src 'self'; img-src *", HtmlMetaScanner.FindHttpEquivContent(html, "Content-Security-Policy"));
        Assert.Equal("5; url=https://example.com", HtmlMetaScanner.FindMetaRefreshContent(html));

        // CSP does not match when http-equiv has trailing whitespace (HTML / CSP §8.1.1 exact match)
        var htmlTrailingSpace = "<meta http-equiv=\"Content-Security-Policy \" content=\"style-src *\">";
        Assert.Null(HtmlMetaScanner.FindCspPolicyContent(htmlTrailingSpace));

        // Refresh DOES match when http-equiv has surrounding whitespace
        var refreshTrailingSpace = "<meta http-equiv=\"  refresh  \" content=\"0; url=/next\">";
        Assert.Equal("0; url=/next", HtmlMetaScanner.FindMetaRefreshContent(refreshTrailingSpace));
    }

    [Fact(Timeout = 600000)]
    public void FindAllHttpEquivContents_Enumerates_All_Matching_Tags()
    {
        var html = """
            <meta http-equiv="refresh" content="1">
            <meta http-equiv="REFRESH" content="2; url=/next">
            <meta http-equiv="other" content="3">
            """;

        var contents = HtmlMetaScanner.FindAllHttpEquivContents(html, "refresh").ToArray();
        Assert.Equal(["1", "2; url=/next"], contents);
    }

    [Fact(Timeout = 600000)]
    public void FindMetaColorScheme_Finds_First_Valid_Outside_Shadow_Tree()
    {
        var doc = new DomDocument();
        var html = doc.CreateElement("html");
        var head = doc.CreateElement("head");

        // Invalid comma-separated content: should be skipped
        var invalidMeta = doc.CreateElement("meta");
        invalidMeta.SetAttribute("name", "color-scheme");
        invalidMeta.SetAttribute("content", "light,dark");
        head.AppendChild(invalidMeta);

        // Valid meta inside shadow root: should be skipped per HTML §4.2.5.3
        var shadowHost = doc.CreateElement("div");
        var shadowRoot = doc.CreateElement("#shadow-root");
        var shadowMeta = doc.CreateElement("meta");
        shadowMeta.SetAttribute("name", "color-scheme");
        shadowMeta.SetAttribute("content", "dark");
        shadowRoot.AppendChild(shadowMeta);
        shadowHost.AppendChild(shadowRoot);
        head.AppendChild(shadowHost);

        // First valid meta in light tree: should be chosen
        var validMeta = doc.CreateElement("meta");
        validMeta.SetAttribute("name", "color-scheme");
        validMeta.SetAttribute("content", "  light dark  ");
        head.AppendChild(validMeta);

        // Later valid meta: should be ignored
        var secondValidMeta = doc.CreateElement("meta");
        secondValidMeta.SetAttribute("name", "color-scheme");
        secondValidMeta.SetAttribute("content", "only dark");
        head.AppendChild(secondValidMeta);

        html.AppendChild(head);
        doc.AppendChild(html);

        Assert.Equal("light dark", HtmlMetaScanner.FindMetaColorScheme(doc));
    }

    [Theory(Timeout = 600000)]
    [InlineData("normal", true)]
    [InlineData("light", true)]
    [InlineData("dark", true)]
    [InlineData("only", true)]
    [InlineData("light dark", true)]
    [InlineData("dark light", true)]
    [InlineData("only light", true)]
    [InlineData("custom-ident_123", true)]
    [InlineData("light,dark", false)]
    [InlineData("light;dark", false)]
    [InlineData("light/dark", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData(null, false)]
    public void IsValidColorSchemeValue_Validates_Css_Syntax(string? content, bool expected)
    {
        Assert.Equal(expected, HtmlMetaScanner.IsValidColorSchemeValue(content));
    }
}
