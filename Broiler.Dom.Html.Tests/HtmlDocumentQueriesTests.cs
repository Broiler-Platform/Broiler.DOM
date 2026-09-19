using Xunit;

namespace Broiler.Dom.Html.Tests;

public sealed class HtmlDocumentQueriesTests
{
    [Fact(Timeout = 600000)]
    public void GetEffectiveBaseHref_DomNode_Finds_First_Base_With_Href()
    {
        var doc = new DomDocument();
        var html = doc.CreateElement("html");
        var head = doc.CreateElement("head");
        var baseWithoutHref = doc.CreateElement("base");
        var baseWithEmptyHref = doc.CreateElement("base");
        baseWithEmptyHref.SetAttribute("href", "   ");
        var baseValid = doc.CreateElement("base");
        baseValid.SetAttribute("href", "  https://example.com/sub/  ");
        var baseSecond = doc.CreateElement("base");
        baseSecond.SetAttribute("href", "https://example.com/other/");

        head.AppendChild(baseWithoutHref);
        head.AppendChild(baseWithEmptyHref);
        head.AppendChild(baseValid);
        head.AppendChild(baseSecond);
        html.AppendChild(head);
        doc.AppendChild(html);

        Assert.Equal("https://example.com/sub/", HtmlDocumentQueries.GetEffectiveBaseHref(doc));
        Assert.True(HtmlDocumentQueries.TryGetEffectiveBaseHref(doc, out var baseHref));
        Assert.Equal("https://example.com/sub/", baseHref);
    }

    [Fact(Timeout = 600000)]
    public void GetEffectiveBaseHref_DomNode_Returns_Null_When_No_Base()
    {
        var doc = new DomDocument();
        var div = doc.CreateElement("div");
        doc.AppendChild(div);

        Assert.Null(HtmlDocumentQueries.GetEffectiveBaseHref(doc));
        Assert.False(HtmlDocumentQueries.TryGetEffectiveBaseHref(doc, out _));
    }

    [Fact(Timeout = 600000)]
    public void GetEffectiveBaseHref_HtmlString_Ignores_Template_And_Returns_First_Valid()
    {
        var html = """
            <!DOCTYPE html>
            <html>
            <head>
                <template><base href="https://ignore-me.com/"></template>
                <base href="  https://example.com/path/  ">
                <base href="https://ignore-second.com/">
            </head>
            </html>
            """;

        Assert.Equal("https://example.com/path/", HtmlDocumentQueries.GetEffectiveBaseHref(html));
        Assert.True(HtmlDocumentQueries.TryGetEffectiveBaseHref(html, out var href));
        Assert.Equal("https://example.com/path/", href);
    }

    [Fact(Timeout = 600000)]
    public void GetEffectiveBaseHref_HtmlString_Returns_Null_When_Absent_Or_Inside_Template_Only()
    {
        Assert.Null(HtmlDocumentQueries.GetEffectiveBaseHref("<html><body>Hello</body></html>"));
        Assert.Null(HtmlDocumentQueries.GetEffectiveBaseHref("<template><base href='https://foo.com'></template>"));
        Assert.Null(HtmlDocumentQueries.GetEffectiveBaseHref((string)null!));
    }

    [Theory(Timeout = 600000)]
    [InlineData("<!DOCTYPE html><html></html>", true)]
    [InlineData("<!doctype HTML><html></html>", true)]
    [InlineData("  \t\r\n <!-- comment --> <!DOCTYPE html>", true)]
    [InlineData("<!-- comment -->\n<!DOCTYPE HTML>", true)]
    [InlineData("<!DOCTYPE html SYSTEM \"about:legacy-compat\">", true)]
    [InlineData("<!DOCTYPE svg>", false)]
    [InlineData("<p><!DOCTYPE html></p>", false)]
    [InlineData("text before <!DOCTYPE html>", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void HasHtmlDoctype_Detects_Standards_Mode_Doctype(string? html, bool expected)
    {
        Assert.Equal(expected, HtmlDocumentQueries.HasHtmlDoctype(html!));
    }
}
