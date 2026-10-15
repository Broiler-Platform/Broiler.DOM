using Broiler.Dom;
using Broiler.Dom.Html;
using Xunit;

namespace Broiler.Dom.Html.Tests;

/// <summary>
/// <c>basefont</c>, <c>bgsound</c> and <c>keygen</c> are no longer conforming, but HTML still parses
/// and serializes them as void: tree construction inserts each one and immediately pops it (HTML
/// §13.2.6.4.4, §13.2.6.4.7), and fragment serialization writes no end tag for them (§13.3).
/// </summary>
/// <remarks>
/// The shared void set used to stop at the fifteen conforming names plus <c>frame</c>, so
/// <c>&lt;keygen&gt;x</c> put the text inside the keygen, a <c>&lt;basefont&gt;</c> in the head was
/// moved into the body and took the rest of the document as its children, and the serializer wrote
/// end tags a browser would never produce. <c>isindex</c> is the opposite case: HTML parses it as an
/// ordinary element now, so it stays out of the set.
/// </remarks>
public sealed class LegacyVoidElementTests
{
    private static string Serialize(string html) =>
        HtmlSerializer.Serialize(HtmlDocumentParser.ParseDocument("<!DOCTYPE html>" + html).Document.DocumentElement!);

    [Theory(Timeout = 600000)]
    [InlineData("basefont")]
    [InlineData("bgsound")]
    [InlineData("keygen")]
    [InlineData("frame")]
    [InlineData("BaseFont")]
    public void The_Shared_Set_Serializes_The_Legacy_Names_As_Void(string name) =>
        Assert.Contains(name, HtmlSerializer.VoidElements);

    [Fact(Timeout = 600000)]
    public void Isindex_Is_No_Longer_Void() =>
        Assert.DoesNotContain("isindex", HtmlSerializer.VoidElements);

    [Theory(Timeout = 600000)]
    [InlineData("<body><keygen>x</keygen>y", "<html><head></head><body><keygen>xy</body></html>")]
    [InlineData("<body><p>a<keygen>b", "<html><head></head><body><p>a<keygen>b</p></body></html>")]
    [InlineData("<body><bgsound>x", "<html><head></head><body><bgsound>x</body></html>")]
    [InlineData("<body><basefont>x", "<html><head></head><body><basefont>x</body></html>")]
    [InlineData("<head><basefont><bgsound></head><body>z", "<html><head><basefont><bgsound></head><body>z</body></html>")]
    [InlineData("<body><isindex>x</isindex>y", "<html><head></head><body><isindex>x</isindex>y</body></html>")]
    public void The_Tree_Builder_Closes_Them_At_Once(string html, string expected) =>
        Assert.Equal(expected, Serialize(html));

    [Theory(Timeout = 600000)]
    [InlineData("basefont")]
    [InlineData("bgsound")]
    [InlineData("keygen")]
    public void A_Script_Built_Element_Is_Written_Without_End_Tag_Or_Children(string name)
    {
        var document = new DomDocument();
        var element = document.CreateElement(name);
        element.AppendChild(document.CreateTextNode("lost"));

        Assert.Equal($"<{name}>", HtmlSerializer.Serialize(element));
    }
}
