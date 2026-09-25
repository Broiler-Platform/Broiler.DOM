using Broiler.Dom;
using Broiler.Dom.Html;
using Xunit;

namespace Broiler.Dom.Html.Tests;

/// <summary>
/// An end tag closes an element only where the "in body" insertion mode (HTML §13.2.6.4.7) lets it:
/// its element must be open and in the scope its rule names, and an end tag with no rule of its own
/// does not reach past a special element. Anything else is ignored — except <c>&lt;/p&gt;</c>, which
/// stands for an empty paragraph, and <c>&lt;/br&gt;</c>, which is a line break.
/// </summary>
/// <remarks>
/// The tree builder used to pop elements until one had the end tag's name, so an end tag that matched
/// nothing emptied the stack down to the body: a single stray <c>&lt;/span&gt;</c> closed every
/// container it was in, and the rest of the page rendered outside them. Each expected tree here is the
/// one a browser builds.
/// </remarks>
public sealed class StrayEndTagTests
{
    private static string Body(string html) =>
        HtmlSerializer.Serialize(HtmlDocumentParser.ParseDocument("<!DOCTYPE html>" + html).Document.Body!);

    [Theory(Timeout = 600000)]
    // Any other end tag, matching nothing: ignored.
    [InlineData("<div>a</span>b</div>c", "<body><div>ab</div>c</body>")]
    [InlineData("a</div>b", "<body>ab</body>")]
    [InlineData("<div>a</td>b</div>", "<body><div>ab</div></body>")]
    // A stray </p> stands for an empty paragraph where it is.
    [InlineData("<div><span>a</p>b</span>c</div>d", "<body><div><span>a<p></p>b</span>c</div>d</body>")]
    [InlineData("<p>a<div>b</p>c</div>", "<body><p>a</p><div>b<p></p>c</div></body>")]
    // "</br>" is a line break.
    [InlineData("a</br>b", "<body>a<br>b</body>")]
    public void An_End_Tag_That_Matches_Nothing_Closes_Nothing(string html, string body) =>
        Assert.Equal(body, Body(html));

    [Theory(Timeout = 600000)]
    // A block's end tag does not reach its element past a table cell.
    [InlineData("<div><table><tr><td>x</div>y</td></tr></table>z</div>",
        "<body><div><table><tbody><tr><td>xy</td></tr></tbody></table>z</div></body>")]
    // An end tag with no rule of its own does not reach past a special element.
    [InlineData("<span><div>x</span>y</div>z", "<body><span><div>xy</div>z</span></body>")]
    [InlineData("<div><svg><g></path></g></svg>x</div>", "<body><div><svg><g></g></svg>x</div></body>")]
    // A list item's end tag does not reach past a nested list.
    [InlineData("<ul><li>a<ol><li>b</li></ol></li>c</ul>", "<body><ul><li>a<ol><li>b</li></ol></li>c</ul></body>")]
    [InlineData("<li>a<ul><li>b</ul>c", "<body><li>a<ul><li>b</li></ul>c</li></body>")]
    public void An_End_Tag_Does_Not_Reach_Past_Its_Scope(string html, string body) =>
        Assert.Equal(body, Body(html));

    [Theory(Timeout = 600000)]
    // A heading's end tag closes whichever heading is open.
    [InlineData("<div><h1>x</h2>y</div>", "<body><div><h1>x</h1>y</div></body>")]
    // What matched before still closes as before.
    [InlineData("<ul><li>a<li>b</ul>c", "<body><ul><li>a</li><li>b</li></ul>c</body>")]
    [InlineData("<div><span>a</div>b", "<body><div><span>a</span></div>b</body>")]
    [InlineData("<table><tr><td>a</table>b", "<body><table><tbody><tr><td>a</td></tr></tbody></table>b</body>")]
    [InlineData("<svg><g><path></path></g></svg>x", "<body><svg><g><path></path></g></svg>x</body>")]
    // The adoption agency algorithm is not modelled: a formatting element's end tag still closes the
    // nearest element of its name and everything open inside it, as it did.
    [InlineData("<b><div>x</b>y</div>", "<body><b><div>x</div></b>y</body>")]
    public void An_End_Tag_In_Reach_Closes_Its_Element(string html, string body) =>
        Assert.Equal(body, Body(html));

    [Fact(Timeout = 600000)]
    public void A_Stray_Paragraph_End_Tag_Before_The_Body_Is_Ignored() =>
        Assert.Equal("<body><p>x</p></body>", Body("</p><p>x</p>"));

    [Fact(Timeout = 600000)]
    public void A_Template_Closes_By_Name_And_A_Stray_One_Is_Ignored()
    {
        var document = HtmlDocumentParser.ParseDocument("<!DOCTYPE html><template><div>x</template>y</template>z").Document;

        var template = Assert.IsType<DomElement>(document.Body!.FirstChild);
        Assert.Equal("template", template.LocalName);
        Assert.Equal("<div>x</div>", HtmlSerializer.Serialize(template.TemplateContents!));
        Assert.Equal("<body><template><div>x</div></template>yz</body>", HtmlSerializer.Serialize(document.Body!));
    }

    /// <summary>
    /// The fragment parsing algorithm (§13.4) never puts the context element on the stack, so the
    /// context's own end tag in the input matches nothing. The string wrapper this builder parses a
    /// fragment in does put it there; end tags must not reach it, or what follows is lost.
    /// </summary>
    [Theory(Timeout = 600000)]
    [InlineData("div", "a</div>b", "ab")]
    [InlineData("td", "<span>a</td>b</span>", "<span>ab</span>")]
    [InlineData("ul", "<li>a</ul>b", "<li>ab</li>")]
    [InlineData("div", "<p>a</div>b</p>c", "<p>ab</p>c")]
    public void A_Fragments_End_Tags_Do_Not_Close_Its_Context(string context, string html, string fragment)
    {
        var result = HtmlDocumentParser.ParseFragment(html, context).Fragment;

        Assert.Equal(fragment, string.Concat(result.ChildNodes.Select(static node => HtmlSerializer.Serialize(node))));
    }
}
